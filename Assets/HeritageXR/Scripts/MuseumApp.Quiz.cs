using System.Linq;
using UnityEngine.UIElements;

namespace HeritageXR
{
    public partial class MuseumApp
    {
        MuseumQuizProgress quizProgress;
        MuseumQuiz ActiveQuiz => selected?.quiz;
        static bool IsQuizPage(MuseumPage page) => page == MuseumPage.QuizIntro || page == MuseumPage.QuizQuestion || page == MuseumPage.QuizFeedback || page == MuseumPage.QuizResult;
        string Q(MuseumQuizText text) => text.In(localization.Language);
        string Number(int number)
        {
            string value = number.ToString();
            if (!localization.IsArabic) return value;
            const string digits = "٠١٢٣٤٥٦٧٨٩";
            return new string(value.Select(c => c >= '0' && c <= '9' ? digits[c - '0'] : c).ToArray());
        }
        string Format(string key, params object[] values) => string.Format(T(key), values);
        MuseumPage ResolveQuizPage(MuseumPage page)
        {
            if (!IsQuizPage(page) || page == MuseumPage.QuizIntro) return page;
            var record = quizProgress.Find(ActiveQuiz);
            if (record == null) return MuseumPage.QuizIntro;
            if (page == MuseumPage.QuizQuestion) return record.feedbackPending ? MuseumPage.QuizFeedback : record.completed ? MuseumPage.QuizResult : page;
            if (page == MuseumPage.QuizFeedback && !record.feedbackPending) return record.completed ? MuseumPage.QuizResult : MuseumPage.QuizIntro;
            if (page == MuseumPage.QuizResult && !record.completed) return MuseumPage.QuizIntro;
            return page;
        }
        void ResetQuizScroll()
        {
            foreach (var page in new[] { MuseumPage.QuizQuestion, MuseumPage.QuizFeedback, MuseumPage.QuizResult }) scrollPositions.Remove((page, selected.id));
        }
        void ResumeQuiz()
        {
            var record = quizProgress.Begin(ActiveQuiz);
            ResetQuizScroll();
            Navigate(record.feedbackPending ? MuseumPage.QuizFeedback : record.completed ? MuseumPage.QuizResult : MuseumPage.QuizQuestion);
        }
        void RestartQuiz()
        {
            quizProgress.Begin(ActiveQuiz, true); ResetQuizScroll();
            // Keep the intro in navigation history: Back leaves the attempt, never undoes an answer.
            Show(MuseumPage.QuizQuestion);
        }
        void QuizIntro()
        {
            var content = Content(); var quiz = ActiveQuiz; var record = quizProgress.Find(quiz);
            content.Add(Photo(selected, "quiz-photo")); content.Add(Words("quiz.eyebrow", "eyebrow"));
            content.Add(Text(Copy(selected).title, "page-title")); content.Add(Text(Format("quiz.intro", Number(quiz.questions.Count)), "intro"));
            content.Add(Words("quiz.rules", "small"));
            if (record != null && record.completedAttempts > 0) content.Add(Text(Format("quiz.best", Number(record.bestScore), Number(quiz.questions.Count)), "quiz-note"));
            string action = record?.feedbackPending == true || record != null && !record.completed && record.answers.Count > 0 ? "quiz.resume" : record?.completed == true ? "quiz.viewResult" : "quiz.start";
            content.Add(Action(T(action), ResumeQuiz, "primary", "quiz-start"));
            content.Add(Action(T("quiz.readFirst"), () => Navigate(MuseumPage.Viewer), "text-link", "quiz-read-first"));
        }
        void QuizQuestion()
        {
            var quiz = ActiveQuiz; var record = quizProgress.Find(quiz);
            var question = quiz.questions[record.answers.Count];
            var content = Content();
            content.Add(Text(Copy(selected).title, "eyebrow")); content.Add(Text(Format("quiz.questionNumber", Number(record.answers.Count + 1), Number(quiz.questions.Count)), "quiz-step"));
            var progress = new ProgressBar { lowValue = 0, highValue = quiz.questions.Count, value = record.answers.Count, title = "" }; progress.AddToClassList("quiz-progress-bar"); content.Add(progress);
            content.Add(Text(Q(question.prompt), "quiz-question"));
            var choices = new VisualElement(); choices.AddToClassList("quiz-options"); content.Add(choices);
            foreach (var option in question.options)
            {
                var answer = option;
                choices.Add(Action(Q(answer.text), () =>
                {
                    // Persistence also rejects duplicates, independently of the UI state.
                    if (CurrentPage != MuseumPage.QuizQuestion) return;
                    if (!quizProgress.Answer(quiz, question.id, answer.id)) return;
                    choices.SetEnabled(false); ResetQuizScroll(); Show(MuseumPage.QuizFeedback);
                }, "quiz-option", "quiz-option-" + answer.id));
            }
            content.Add(Words("quiz.oneAnswer", "quiz-note"));
        }
        void QuizFeedback()
        {
            var quiz = ActiveQuiz; var record = quizProgress.Find(quiz);
            int index = record.answers.Count - 1;
            var question = quiz.questions[index]; var answer = record.answers[index]; bool correct = answer.optionId == question.correctOptionId;
            var content = Content(); content.Add(Text(Format("quiz.questionNumber", Number(index + 1), Number(quiz.questions.Count)), "quiz-step"));
            content.Add(Words(correct ? "quiz.correct" : "quiz.incorrect", "page-title"));
            content.Add(Text(Q(question.prompt), "quiz-question"));
            var card = new VisualElement(); card.AddToClassList("quiz-feedback");
            card.Add(Text(Format("quiz.yourAnswer", Q(question.options.Find(o => o.id == answer.optionId).text)), "small"));
            if (!correct) card.Add(Text(Format("quiz.correctAnswer", Q(question.options.Find(o => o.id == question.correctOptionId).text)), "quiz-answer"));
            card.Add(Text(Q(question.explanation))); content.Add(card);
            content.Add(Action(T(record.completed ? "quiz.viewResult" : "quiz.next"), () =>
            {
                if (CurrentPage != MuseumPage.QuizFeedback) return;
                quizProgress.Continue(quiz); ResetQuizScroll(); Show(record.completed ? MuseumPage.QuizResult : MuseumPage.QuizQuestion);
            }, "primary", "quiz-next"));
        }
        void QuizResult()
        {
            var quiz = ActiveQuiz; var record = quizProgress.Find(quiz);
            var content = Content(); content.Add(Text(Copy(selected).title, "eyebrow")); content.Add(Words("quiz.result", "page-title"));
            int score = quizProgress.Score(quiz, record);
            content.Add(Text(Number(score) + " / " + Number(quiz.questions.Count), "quiz-score"));
            content.Add(Words(score == quiz.questions.Count ? "quiz.resultPerfect" : "quiz.resultLearning", "intro"));
            content.Add(Text(Format("quiz.best", Number(record.bestScore), Number(quiz.questions.Count)), "quiz-note"));
            content.Add(Action(T("quiz.retry"), RestartQuiz, "primary", "quiz-retry"));
            content.Add(Action(T("quiz.progress.open"), () => Navigate(MuseumPage.QuizProgress), "secondary", "quiz-result-progress"));
            // Replace the quiz flow with its source page, rather than stacking old result screens.
            content.Add(Action(T("quiz.backArtwork"), () => { history.Clear(); history.Push((MuseumPage.Collection, null)); Show(MuseumPage.Detail); }, "text-link", "quiz-back-artwork"));
        }
        void QuizProgress()
        {
            var content = Content(); content.Add(Words("quiz.progress.title", "page-title"));
            var artworks = collection.artworks.Where(a => a.quiz != null).ToArray();
            int completed = artworks.Count(a => quizProgress.Find(a.quiz)?.completedAttempts > 0);
            int best = artworks.Sum(a => quizProgress.Find(a.quiz)?.bestScore ?? 0), total = artworks.Sum(a => a.quiz.questions.Count);
            content.Add(Text(Format("quiz.progress.summary", Number(completed), Number(artworks.Length)), "intro"));
            content.Add(Text(Format("quiz.progress.bestTotal", Number(best), Number(total)), "quiz-note"));
            foreach (var artwork in artworks)
            {
                var record = quizProgress.Find(artwork.quiz);
                var card = new Button(() => Navigate(MuseumPage.QuizIntro, artwork.id)) { name = "quiz-progress-" + artwork.id };
                card.AddToClassList("quiz-progress-card"); card.Add(Text(Copy(artwork).title, "card-title"));
                string state = record != null && !record.completed && (record.answers.Count > 0 || record.completedAttempts > 0) ? Format("quiz.progress.inProgress", Number(record.answers.Count), Number(artwork.quiz.questions.Count)) : record?.completed == true ? T("quiz.progress.completed") : T("quiz.progress.notStarted");
                card.Add(Text(state, "small"));
                if (record?.completedAttempts > 0) card.Add(Text(Format("quiz.best", Number(record.bestScore), Number(artwork.quiz.questions.Count)), "quiz-note"));
                content.Add(card);
            }
            content.Add(Words("quiz.progress.local", "small"));
        }
    }
}
