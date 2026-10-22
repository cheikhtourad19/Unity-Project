"""Align existing scripts to existing recordings; never estimate evenly spaced timing."""
import argparse, hashlib, json, re, textwrap
from pathlib import Path
import numpy as np
import soundfile as sf
import torch
import torchaudio
import uroman


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def dump(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n')


def emissions_for(model, waveform, device):
    # Bound inference memory. Overlapping acoustic context is removed by frame index,
    # preserving the original recording clock; words are aligned over the whole clip.
    hop=320; window=12*16000; context=16000
    total_frames=(waveform.shape[1]-400)//hop+1
    blocks=[]
    for start in range(0,waveform.shape[1],window):
        left=max(0,start-context);right=min(waveform.shape[1],start+window+context)
        emission,_=model(waveform[:,left:right].to(device))
        first=start//hop;last=min((start+window)//hop,total_frames)
        if last<=first:continue
        local=(start-left)//hop
        blocks.append(emission[:,local:local+last-first].cpu())
    result=torch.cat(blocks,dim=1)
    assert result.shape[1]==total_frames
    return result


def caption_chunks(words):
    groups=[]; current=[]
    for word in words:
        candidate=current+[word]
        text=' '.join(x['text'] for x in candidate)
        if current and (len(text)>56 or word['endSeconds']-current[0]['startSeconds']>5.5 or word['startSeconds']-current[-1]['endSeconds']>.65):
            groups.append(current); current=[]
        current.append(word)
        if len(' '.join(x['text'] for x in current))>=32 and re.search(r'[.!?؟؛;]$',word['text']):
            groups.append(current);current=[]
    if current:groups.append(current)
    cues=[]
    for i,group in enumerate(groups):
        text=' '.join(x['text'] for x in group)
        lines=textwrap.wrap(text, width=28, break_long_words=False, break_on_hyphens=False)
        if len(lines)>2:
            # Prefer a balanced two-line layout when a long word prevents 34-column wrap.
            parts=text.split(); cut=min(range(1,len(parts)),key=lambda j:abs(len(' '.join(parts[:j]))-len(' '.join(parts[j:])))) if len(parts)>1 else 0
            lines=[' '.join(parts[:cut]),' '.join(parts[cut:])] if cut else [text]
        start=group[0]['startSeconds'];end=group[-1]['endSeconds']
        next_start=groups[i+1][0]['startSeconds'] if i+1<len(groups) else end+.2
        # Small display grace after the last actual word, never into the following cue.
        end=min(next_start,max(end+.08,start+.8))
        cues.append({'startSeconds':round(start,3),'endSeconds':round(end,3),'text':'\n'.join(lines)})
    return cues


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);parser.add_argument('--only');parser.add_argument('--device',default='cpu');args=parser.parse_args()
    root=args.root;catalog=json.loads((root/'Content/catalog.json').read_text());romanizer=uroman.Uroman();torch.set_num_threads(4)
    bundle=torchaudio.pipelines.MMS_FA
    print('Loading multilingual alignment model; original text/audio remain unchanged.',flush=True)
    if args.device=='mps':torch.mps.set_per_process_memory_fraction(.45)
    model=bundle.get_model().to(args.device).eval();tokenizer=bundle.get_tokenizer();aligner=bundle.get_aligner()
    reports=[]
    for artwork in catalog['artworks']:
        for track in artwork['audio']:
            if args.only and args.only!=track['id']:continue
            source=root/track['sourceAudioPath'];script=root/track['scriptPath'];text=script.read_text().strip()
            target=root/'Content/Subtitles'/artwork['id']/(track['kind']+'.'+track['language']+'.json')
            if target.exists():
                old=json.loads(target.read_text())
                if old.get('audioSha256')==sha(source) and old.get('scriptSha256')==sha(script):
                    reports.append(old['quality']);print('Reusing',track['id'],flush=True);continue
            original=re.findall(r'\S+',text)
            normalized=[re.sub(r"[^a-z']",'',romanizer.romanize_string(w,lcode={'fr':'fra','ar':'ara','en':'eng'}[track['language']]).lower().replace('’',"'")) for w in original]
            combined=[];spoken=[]
            for word,norm in zip(original,normalized):
                if not norm and combined and not re.search(r'\w',word):
                    combined[-1]+=' '+word
                elif not norm:raise ValueError('Unsupported normalized word: '+track['id']+' '+word)
                else:combined.append(word);spoken.append(norm)
            original,normalized=combined,spoken
            data,sr=sf.read(source,dtype='float32',always_2d=True)
            waveform=torch.from_numpy(data.mean(axis=1).copy()).unsqueeze(0)
            if sr!=bundle.sample_rate:waveform=torchaudio.functional.resample(waveform,sr,bundle.sample_rate)
            with torch.inference_mode():
                emission=emissions_for(model,waveform,args.device)
                spans=aligner(emission[0].cpu(),tokenizer(normalized))
            ratio=waveform.shape[1]/bundle.sample_rate/emission.shape[1]
            words=[]
            for original_word,word_spans in zip(original,spans):
                confidence=sum(s.score*len(s) for s in word_spans)/sum(len(s) for s in word_spans)
                words.append({'text':original_word,'startSeconds':round(word_spans[0].start*ratio,4),'endSeconds':round(word_spans[-1].end*ratio,4),'alignmentScore':round(float(confidence),4)})
            assert len(words)==len(original)
            segments=caption_chunks(words)
            for cue in segments:cue['endSeconds']=min(cue['endSeconds'],track['durationSeconds'])
            assert all(0<=c['startSeconds']<c['endSeconds']<=track['durationSeconds'] for c in segments)
            assert all(x['endSeconds']<=y['startSeconds'] for x,y in zip(segments,segments[1:]))
            assert ' '.join(' '.join(c['text'].split()) for c in segments)==' '.join(text.split())
            quality={'trackId':track['id'],'wordCount':len(words),'cueCount':len(segments),'meanAlignmentScore':round(float(np.mean([w['alignmentScore'] for w in words])),4),'lowScoreWords':[w for w in words if w['alignmentScore']<.08],'longWords':[w for w in words if w['endSeconds']-w['startSeconds']>2.5],'userListeningReview':'pending'}
            result={'schemaVersion':1,'trackId':track['id'],'language':track['language'],'kind':track['kind'],'audioSha256':sha(source),'scriptSha256':sha(script),'durationSeconds':track['durationSeconds'],'alignment':{'method':'mms_ctc_forced_alignment','model':'torchaudio.pipelines.MMS_FA','torchVersion':torch.__version__,'torchaudioVersion':torchaudio.__version__,'normalization':'uroman; a-z and apostrophes for alignment only','timingResolutionSeconds':round(ratio,6),'documentation':'https://docs.pytorch.org/audio/2.8/tutorials/forced_alignment_for_multilingual_data_tutorial.html'},'reviewStatus':'automatically_aligned_pending_user_review','segments':segments,'words':words,'quality':quality}
            dump(target,result);reports.append(quality);print(track['id'],'words',len(words),'cues',len(segments),'score',quality['meanAlignmentScore'],'low',len(quality['lowScoreWords']),flush=True)
    dump(root/'Content/Subtitles/alignment-report.json',{'method':'mms_ctc_forced_alignment','processedCount':len(reports),'tracks':reports,'functionalTestsExecuted':False,'humanListeningReviewPerformed':False})

if __name__=='__main__':main()
