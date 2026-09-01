namespace CatLife.LLM
{
    public enum LlmRuntimeMode
    {
        Auto = 0,
        MockOrGenericCloud = 1,
        BlueLmOnDevice = 2,
        LocalTemplateOnly = 3
    }
}
