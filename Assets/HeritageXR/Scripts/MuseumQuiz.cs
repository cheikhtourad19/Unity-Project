using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HeritageXR
{
    [Serializable]
    public sealed class MuseumQuizText
    {
        public string fr, ar, en;
        public string In(string language) => language == "ar" ? ar : language == "en" ? en : fr;
    }
    [Serializable]
    public sealed class MuseumQuizOption
    {
        public string id;
        public MuseumQuizText text;
    }
    [Serializable]
    public sealed class MuseumQuizQuestion
    {
        public string id, correctOptionId;
        public MuseumQuizText prompt, explanation;
        public List<MuseumQuizOption> options = new();
    }
    [Serializable]
    public sealed class MuseumQuiz
    {
        public string id;
        public int contentVersion;
        public List<MuseumQuizQuestion> questions = new();
    }
    [Serializable]
    public sealed class MuseumQuizAnswer { public string questionId, optionId; }
    [Serializable]
    public sealed class MuseumQuizRecord
    {
        public string quizId;
        public int contentVersion, bestScore, completedAttempts;
        public bool completed, feedbackPending;
        public List<MuseumQuizAnswer> answers = new();
    }

    // One persisted record per quiz revision: current attempt, last result and best score.
    // Answer IDs survive language changes; a changed content revision starts cleanly.
    public sealed class MuseumQuizProgress
    {
        [Serializable] sealed class Store { public int schemaVersion = 1; public List<MuseumQuizRecord> records = new(); }
        const string Key = "heritage.quizProgress.v1", BackupKey = "heritage.quizProgress.v1.backup";
        Store store;
        public MuseumQuizProgress()
        {
            store = Read(PlayerPrefs.GetString(Key, "")) ?? Read(PlayerPrefs.GetString(BackupKey, "")) ?? new Store();
        }
        static Store Read(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var parsed = JsonUtility.FromJson<Store>(json);
                if (parsed == null || parsed.schemaVersion != 1 || parsed.records == null || parsed.records.Any(r => r == null || string.IsNullOrEmpty(r.quizId))) return null;
                if (parsed.records.Select(r => r.quizId).Distinct().Count() != parsed.records.Count) return null;
                return parsed;
            }
            catch (ArgumentException) { return null; }
        }
        bool Valid(MuseumQuizRecord record, MuseumQuiz quiz)
        {
            if (record.contentVersion != quiz.contentVersion || record.answers == null || record.answers.Count > quiz.questions.Count || record.bestScore < 0 || record.bestScore > quiz.questions.Count || record.completedAttempts < 0) return false;
            if (record.completed != (record.answers.Count == quiz.questions.Count) || record.feedbackPending && record.answers.Count == 0) return false;
            for (int i = 0; i < record.answers.Count; i++)
                if (record.answers[i] == null || record.answers[i].questionId != quiz.questions[i].id || !quiz.questions[i].options.Any(o => o.id == record.answers[i].optionId)) return false;
            return true;
        }
        public MuseumQuizRecord Find(MuseumQuiz quiz)
        {
            if (quiz == null) return null;
            var record = store.records.Find(r => r.quizId == quiz.id);
            return record != null && Valid(record, quiz) ? record : null;
        }
        public MuseumQuizRecord Begin(MuseumQuiz quiz, bool restart = false)
        {
            var previous = Find(quiz);
            if (previous != null && !restart) return previous;
            var record = new MuseumQuizRecord { quizId = quiz.id, contentVersion = quiz.contentVersion, bestScore = previous?.bestScore ?? 0, completedAttempts = previous?.completedAttempts ?? 0 };
            store.records.RemoveAll(r => r.quizId == quiz.id); store.records.Add(record); Save(); return record;
        }
        public bool Answer(MuseumQuiz quiz, string questionId, string optionId)
        {
            var record = Find(quiz);
            if (record == null || record.completed || record.feedbackPending) return false;
            var question = quiz.questions[record.answers.Count];
            if (question.id != questionId || !question.options.Any(o => o.id == optionId)) return false;
            record.answers.Add(new MuseumQuizAnswer { questionId = questionId, optionId = optionId }); record.feedbackPending = true;
            if (record.answers.Count == quiz.questions.Count)
            {
                record.completed = true; record.completedAttempts++;
                record.bestScore = Mathf.Max(record.bestScore, Score(quiz, record));
            }
            Save(); return true;
        }
        public void Continue(MuseumQuiz quiz)
        {
            var record = Find(quiz);
            if (record == null || !record.feedbackPending) return;
            record.feedbackPending = false; Save();
        }
        public int Score(MuseumQuiz quiz, MuseumQuizRecord record)
        {
            if (record == null) return 0;
            return record.answers.Count(a => quiz.questions.Any(q => q.id == a.questionId && q.correctOptionId == a.optionId));
        }
        void Save()
        {
            string previous = PlayerPrefs.GetString(Key, "");
            if (Read(previous) != null) PlayerPrefs.SetString(BackupKey, previous);
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(store)); PlayerPrefs.Save();
        }
    }
}
