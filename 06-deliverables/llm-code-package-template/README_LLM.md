# CatLife Large Model Code Package Template

This package is a reviewable template for the competition code bundle. It demonstrates where the large-model API call belongs, how privacy filtering works, and how CatLife degrades to local templates when no network or key is available.

Current status: Unity runtime wiring now exists in `work/CatLife_Unity_Main/Assets/Scripts/LLM/`.
The package remains a reviewable competition handoff template, but its DTOs, structured output contract,
privacy gate, timeout fallback and local fallback match the current Unity implementation.

## Files

| File | Purpose |
|---|---|
| `src/BehaviorFeatureSummary.cs` | Aggregated, non-sensitive session feature DTO |
| `src/FocusFeedback.cs` | Feedback output DTO |
| `src/FocusFeedbackLlmOutput.cs` | Structured LLM response DTO and safety gate |
| `src/IFocusFeedbackProvider.cs` | Provider interface |
| `src/PrivacyGateway.cs` | Allow-list based validation before model calls |
| `src/LocalTemplateFallback.cs` | Offline/no-key fallback text generation |
| `src/LLMExplainClient.cs` | API-call boundary with timeout and fallback |
| `prompts/prompt_focus_explain.md` | Prompt template |
| `samples/sample_feature_summary.json` | Safe sample input |
| `samples/sample_llm_response.json` | Safe sample output |
| `work/CatLife_Unity_Main/Assets/Configs/llm_feedback_schema.json` | Unity-side JSON schema used by runtime validation |

## Privacy Boundary

Allowed input:

- session duration
- aggregate focus/arousal/distraction scores
- focus block count
- interruption count
- state sequence
- optional user-visible goal text

Forbidden input:

- raw typed content
- raw tap coordinates
- screenshots or OCR
- cross-app behavior
- precise location
- identifiers such as phone number, student ID, email, token, cookie

## API Key Handling

Do not put keys in this package. The integration layer should read a key from runtime configuration or environment variables outside version control.

If no key is configured, use `LocalTemplateFallback`.

## Review Demo

Expected demo flow:

```text
sample_feature_summary.json -> PrivacyGateway -> LLMExplainClient
                                    | no key / timeout / invalid input
                                    v
                              LocalTemplateFallback
```

The model output should be short, non-judgmental, and aligned with CatLife's companion design.
The accepted output shape is strict:

```json
{
  "schema_version": "catlife.focus_feedback.v1",
  "bubble_text": "刚才这段很稳，我会继续安静陪你。",
  "record_summary": "本轮节奏稳定，猫咪已降低动作频率，陪你完成这段专注。",
  "tone": "warm",
  "reaction_hint": "tail_wag_happy",
  "confidence": 0.84,
  "safety": {
    "contains_blame": false,
    "contains_medical_claim": false,
    "contains_sensitive_inference": false
  }
}
```

The runtime rejects output when schema version mismatches, confidence is below `0.5`,
any safety flag is true, or text contains blocked command/privacy wording such as
coordinates, Transform, NavMesh, Animator, screenshots, raw input or cross-app content.
