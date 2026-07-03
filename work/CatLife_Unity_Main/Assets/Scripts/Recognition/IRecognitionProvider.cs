namespace CatLife.Recognition
{
    public interface IRecognitionProvider
    {
        bool IsReady { get; }
        float PollIntervalSeconds { get; }
        RecognitionSnapshot Latest { get; }

        void Initialize();
        void Tick(float unscaledDeltaTime);
    }
}
