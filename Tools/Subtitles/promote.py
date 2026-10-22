"""Promote all aligned subtitle files together, preserving publication review flags."""
import hashlib,json,re
from pathlib import Path
root=Path(__file__).resolve().parents[2]
path=root/'Content/catalog.json';catalog=json.loads(path.read_text())
count=0;total=0
for artwork in catalog['artworks']:
    for track in artwork['audio']:
        file=root/'Content/Subtitles'/artwork['id']/(track['kind']+'.'+track['language']+'.json')
        document=json.loads(file.read_text())
        assert document['trackId']==track['id']
        assert document['audioSha256']==hashlib.sha256((root/track['sourceAudioPath']).read_bytes()).hexdigest()
        assert document['scriptSha256']==track['scriptSha256']
        segments=document['segments'];assert segments
        previous=0
        for cue in segments:
            assert previous<=cue['startSeconds']<cue['endSeconds']<=track['durationSeconds']
            assert len(cue['text'].splitlines())<=2
            previous=cue['endSeconds']
        assert re.sub(r'\s+',' ',' '.join(c['text'] for c in segments)).strip()==re.sub(r'\s+',' ',(root/track['scriptPath']).read_text()).strip()
        track['subtitlesPath']=str(file.relative_to(root));track['subtitleSegments']=segments;track['subtitlesStatus']='draft'
        count+=1;total+=len(segments)
    artwork['readiness']['subtitlesAligned']=True
    assert artwork['readiness']['readyForPublication'] is False
catalog['catalogVersion']='1.0.0-prototype.2';catalog['status']='subtitles_integrated_pending_user_review'
path.write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n')
print('Promoted',count,'subtitle tracks;',total,'cues. Automatic alignment; user listening review remains pending.')
