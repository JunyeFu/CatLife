package com.catlife.bluelm;

import android.content.Context;
import java.io.File;

public final class BlueLmEngine {
    public static final String DEFAULT_MODEL_PATH = "/sdcard/1225/1.7.0.4_1225_mtk9500";
    public static final int CODE_OK = 0;
    public static final int CODE_MODEL_PATH_MISSING = 1101;
    public static final int CODE_PERMISSION_MISSING = 1102;
    public static final int CODE_SDK_NOT_LINKED = 1201;
    public static final int CODE_GENERATE_FAILED = 1202;
    public static final int CODE_BAD_REQUEST = 1301;
    public static final int CODE_UNSAFE_OUTPUT = 1302;

    private static String modelPath = DEFAULT_MODEL_PATH;
    private static boolean modelPathAvailable;
    private static BlueLmSdkAdapter sdkAdapter;

    private BlueLmEngine() {
    }

    public static synchronized InitResult init(Context context, String requestedModelPath) {
        modelPath = normalizePath(requestedModelPath);
        if (!BlueLmPermissionHelper.hasManageAllFilesAccess()) {
            modelPathAvailable = false;
            sdkAdapter = null;
            return new InitResult(false, CODE_PERMISSION_MISSING, "ALL_FILES_ACCESS_MISSING", modelPath);
        }

        File dir = new File(modelPath);
        modelPathAvailable = dir.exists() && dir.isDirectory();
        if (!modelPathAvailable) {
            sdkAdapter = null;
            return new InitResult(false, CODE_MODEL_PATH_MISSING, "MODEL_PATH_MISSING", modelPath);
        }

        BlueLmSdkAdapter adapter = new BlueLmSdkAdapter();
        BlueLmSdkAdapter.InitOutcome outcome = adapter.init(modelPath);
        if (!outcome.ok) {
            sdkAdapter = null;
            return new InitResult(false, outcome.code, outcome.message, modelPath);
        }

        sdkAdapter = adapter;
        return new InitResult(true, CODE_OK, "OK", modelPath);
    }

    public static void generateJsonAsync(String requestJson, GenerateCallback callback) {
        final String safeRequestJson = requestJson == null ? "" : requestJson;
        final GenerateCallback safeCallback = callback;
        new Thread(new Runnable() {
            @Override
            public void run() {
                GuardResult inputGuard = BlueLmJsonGuard.validateRequest(safeRequestJson);
                String requestId = inputGuard.requestId;
                if (requestId.length() == 0) {
                    if (safeCallback != null) {
                        safeCallback.onComplete("", false, CODE_BAD_REQUEST, "REQUEST_ID_MISSING", "");
                    }
                    return;
                }

                if (!inputGuard.ok) {
                    if (safeCallback != null) {
                        safeCallback.onComplete(requestId, false, CODE_BAD_REQUEST, inputGuard.reason, "");
                    }
                    return;
                }

                if (!modelPathAvailable) {
                    if (safeCallback != null) {
                        safeCallback.onComplete(requestId, false, CODE_MODEL_PATH_MISSING, "MODEL_PATH_MISSING", "");
                    }
                    return;
                }

                BlueLmSdkAdapter adapter = sdkAdapter;
                if (adapter == null || !adapter.isReady()) {
                    if (safeCallback != null) {
                        safeCallback.onComplete(requestId, false, CODE_SDK_NOT_LINKED, "SDK_NOT_LINKED", BlueLmPromptBuilder.buildPromptEnvelope(safeRequestJson));
                    }
                    return;
                }

                String prompt = BlueLmPromptBuilder.buildPromptEnvelope(safeRequestJson) + "\n" + BlueLmPromptBuilder.outputSchemaReminder();
                adapter.generate(prompt, new BlueLmSdkAdapter.GenerateOutcomeCallback() {
                    @Override
                    public void onComplete(BlueLmSdkAdapter.GenerateOutcome outcome) {
                        if (safeCallback == null) {
                            return;
                        }

                        if (outcome == null || !outcome.ok) {
                            String reason = outcome == null ? "GENERATE_EMPTY_OUTCOME" : outcome.error;
                            safeCallback.onComplete(requestId, false, CODE_GENERATE_FAILED, reason, safeFallbackJson());
                            return;
                        }

                        String outputJson = BlueLmJsonGuard.extractJson(outcome.text);
                        GuardResult outputGuard = BlueLmJsonGuard.validateOutput(outputJson);
                        if (!outputGuard.ok) {
                            safeCallback.onComplete(requestId, false, CODE_UNSAFE_OUTPUT, outputGuard.reason, safeFallbackJson());
                            return;
                        }

                        safeCallback.onComplete(requestId, true, CODE_OK, "OK", outputJson);
                    }
                });
            }
        }, "CatLifeBlueLmGenerate").start();
    }

    private static String normalizePath(String requestedModelPath) {
        if (requestedModelPath == null || requestedModelPath.trim().length() == 0) {
            return DEFAULT_MODEL_PATH;
        }

        return requestedModelPath.trim();
    }

    private static String safeFallbackJson() {
        return "{" +
            "\"version\":\"catlife.bluelm.feedback.v1\"," +
            "\"suggestedLine\":\"\"," +
            "\"showBubble\":false," +
            "\"moodBias\":\"quiet\"," +
            "\"roamWeightBias\":0.0," +
            "\"quietIdleWeightBias\":0.0," +
            "\"socialResponseWeightBias\":0.0," +
            "\"recommendedLocalAction\":\"none\"," +
            "\"rawTextRequested\":false," +
            "\"coordinateCommandIncluded\":false," +
            "\"animatorCommandIncluded\":false," +
            "\"navMeshCommandIncluded\":false," +
            "\"transformCommandIncluded\":false," +
            "\"privacyInferenceIncluded\":false" +
            "}";
    }

    public interface GenerateCallback {
        void onComplete(String requestId, boolean ok, int code, String message, String responseJson);
    }

    public static final class GuardResult {
        public final boolean ok;
        public final String requestId;
        public final String reason;

        public GuardResult(boolean ok, String requestId, String reason) {
            this.ok = ok;
            this.requestId = requestId == null ? "" : requestId;
            this.reason = reason == null ? "" : reason;
        }
    }

    public static final class InitResult {
        public final boolean ok;
        public final int code;
        public final String message;
        public final String modelPath;

        public InitResult(boolean ok, int code, String message, String modelPath) {
            this.ok = ok;
            this.code = code;
            this.message = message;
            this.modelPath = modelPath;
        }
    }
}
