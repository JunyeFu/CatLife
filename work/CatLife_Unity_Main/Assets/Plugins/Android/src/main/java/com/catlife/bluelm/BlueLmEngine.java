package com.catlife.bluelm;

import android.content.Context;
import java.io.File;
import org.json.JSONObject;

public final class BlueLmEngine {
    public static final String DEFAULT_MODEL_PATH = "/sdcard/1225/1.7.0.4_1225_mtk9500";
    public static final int CODE_OK = 0;
    public static final int CODE_MODEL_PATH_MISSING = 1101;
    public static final int CODE_PERMISSION_MISSING = 1102;
    public static final int CODE_SDK_NOT_LINKED = 1201;
    public static final int CODE_BAD_REQUEST = 1301;

    private static String modelPath = DEFAULT_MODEL_PATH;
    private static boolean modelPathAvailable;
    private static boolean sdkLinked;

    private BlueLmEngine() {
    }

    public static synchronized InitResult init(Context context, String requestedModelPath) {
        modelPath = normalizePath(requestedModelPath);
        if (!BlueLmPermissionHelper.hasManageAllFilesAccess()) {
            modelPathAvailable = false;
            sdkLinked = false;
            return new InitResult(false, CODE_PERMISSION_MISSING, "ALL_FILES_ACCESS_MISSING", modelPath);
        }

        File dir = new File(modelPath);
        modelPathAvailable = dir.exists() && dir.isDirectory();
        if (!modelPathAvailable) {
            sdkLinked = false;
            return new InitResult(false, CODE_MODEL_PATH_MISSING, "MODEL_PATH_MISSING", modelPath);
        }

        sdkLinked = false;
        return new InitResult(false, CODE_SDK_NOT_LINKED, "SDK_NOT_LINKED", modelPath);
    }

    public static void generateJsonAsync(String requestJson, GenerateCallback callback) {
        final String safeRequestJson = requestJson == null ? "" : requestJson;
        final GenerateCallback safeCallback = callback;
        new Thread(new Runnable() {
            @Override
            public void run() {
                String requestId = extractRequestId(safeRequestJson);
                if (requestId.length() == 0) {
                    if (safeCallback != null) {
                        safeCallback.onComplete("", false, CODE_BAD_REQUEST, "REQUEST_ID_MISSING", "");
                    }
                    return;
                }

                if (!modelPathAvailable) {
                    if (safeCallback != null) {
                        safeCallback.onComplete(requestId, false, CODE_MODEL_PATH_MISSING, "MODEL_PATH_MISSING", "");
                    }
                    return;
                }

                if (!sdkLinked) {
                    if (safeCallback != null) {
                        safeCallback.onComplete(requestId, false, CODE_SDK_NOT_LINKED, "SDK_NOT_LINKED", "");
                    }
                    return;
                }

                if (safeCallback != null) {
                    safeCallback.onComplete(requestId, false, CODE_SDK_NOT_LINKED, "SDK_NOT_LINKED", "");
                }
            }
        }, "CatLifeBlueLmGenerate").start();
    }

    private static String extractRequestId(String requestJson) {
        if (requestJson == null || requestJson.length() == 0) {
            return "";
        }

        try {
            JSONObject object = new JSONObject(requestJson);
            return object.optString("requestId", "");
        } catch (Throwable ignored) {
            return "";
        }
    }

    private static String normalizePath(String requestedModelPath) {
        if (requestedModelPath == null || requestedModelPath.trim().length() == 0) {
            return DEFAULT_MODEL_PATH;
        }

        return requestedModelPath.trim();
    }

    public interface GenerateCallback {
        void onComplete(String requestId, boolean ok, int code, String message, String responseJson);
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
