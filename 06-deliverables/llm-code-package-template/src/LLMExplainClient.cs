using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CatLife.Llm
{
    public sealed class LLMExplainClient : IFocusFeedbackProvider
    {
        private readonly HttpClient httpClient;
        private readonly PrivacyGateway privacyGateway;
        private readonly IFocusFeedbackProvider fallback;
        private readonly string endpoint;
        private readonly Func<string> apiKeyProvider;

        public LLMExplainClient(
            HttpClient httpClient,
            PrivacyGateway privacyGateway,
            IFocusFeedbackProvider fallback,
            string endpoint,
            Func<string> apiKeyProvider)
        {
            this.httpClient = httpClient;
            this.privacyGateway = privacyGateway;
            this.fallback = fallback;
            this.endpoint = endpoint;
            this.apiKeyProvider = apiKeyProvider;
        }

        public async Task<FocusFeedback> GenerateAsync(
            BehaviorFeatureSummary summary,
            CancellationToken cancellationToken)
        {
            if (!privacyGateway.CanSend(summary, out _))
            {
                return await fallback.GenerateAsync(summary, cancellationToken);
            }

            string apiKey = apiKeyProvider?.Invoke() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return await fallback.GenerateAsync(summary, cancellationToken);
            }

            using (var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token))
            {
                try
                {
                    string payload = BuildPayload(summary);
                    using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                    {
                        request.Headers.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                        using (HttpResponseMessage response = await httpClient.SendAsync(request, linked.Token))
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                return await fallback.GenerateAsync(summary, cancellationToken);
                            }

                            string body = await response.Content.ReadAsStringAsync();
                            if (!TryExtractStructuredFeedback(body, out FocusFeedback feedback))
                            {
                                return await fallback.GenerateAsync(summary, cancellationToken);
                            }

                            return feedback;
                        }
                    }
                }
                catch
                {
                    return await fallback.GenerateAsync(summary, cancellationToken);
                }
            }
        }

        private static string BuildPayload(BehaviorFeatureSummary summary)
        {
            string states = string.Join(",", summary.CatStateSequence ?? new System.Collections.Generic.List<string>());
            string goal = Escape(summary.UserVisibleGoal ?? "未填写");

            return "{"
                + "\"model\":\"demo-model\","
                + "\"messages\":["
                + "{\"role\":\"system\",\"content\":\"你是 CatLife 的陪伴式专注反馈助手。只输出严格 JSON。\"},"
                + "{\"role\":\"user\",\"content\":\"请根据去标识化摘要生成 catlife.focus_feedback.v1 JSON。"
                + "duration=" + summary.DurationSec
                + ",focus=" + summary.FocusScoreAvg
                + ",arousal=" + summary.ArousalScoreAvg
                + ",distraction=" + summary.DistractionScoreAvg
                + ",interrupts=" + summary.InterruptCount
                + ",states=" + Escape(states)
                + ",goal=" + goal
                + "\"}"
                + "]"
                + "}";
        }

        private static bool TryExtractStructuredFeedback(string responseBody, out FocusFeedback feedback)
        {
            feedback = FocusFeedback.Local("这段记录已保存，猫咪会继续安静陪你。");
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return false;
            }

            try
            {
                FocusFeedbackLlmOutput output = JsonSerializer.Deserialize<FocusFeedbackLlmOutput>(
                    responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return FocusFeedbackLlmOutput.TryBuildFeedback(output, out feedback, out _);
            }
            catch
            {
                return false;
            }
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
