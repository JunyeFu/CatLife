using System;

namespace CatLife.LLM
{
    public interface ICatLLMClient
    {
        bool Enabled { get; }
        bool IsBusy { get; }

        void RequestSuggestion(
            CatPromptContext context,
            CatPromptBuilder builder,
            Action<LLMBehaviorSuggestion> onSuccess,
            Action<string> onError);
    }
}
