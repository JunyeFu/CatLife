package com.catlife.bluelm;

import android.app.Activity;
import com.unity3d.player.UnityPlayer;

public final class BlueLmUnityBridge {
    private BlueLmUnityBridge() {
    }

    public static void init(String callbackGameObjectName, String modelPath) {
        Activity activity = UnityPlayer.currentActivity;
        BlueLmEngine.InitResult result = BlueLmEngine.init(activity, modelPath);
        BlueLmUnityCallback.sendInit(callbackGameObjectName, result.ok, result.code, result.message, result.modelPath);
    }

    public static void generate(String requestJson, String callbackGameObjectName) {
        BlueLmEngine.generateJsonAsync(requestJson, new BlueLmEngine.GenerateCallback() {
            @Override
            public void onComplete(String requestId, boolean ok, int code, String message, String responseJson) {
                BlueLmUnityCallback.sendGenerate(callbackGameObjectName, requestId, ok, code, message, responseJson);
            }
        });
    }

    public static boolean hasManageAllFilesAccess() {
        return BlueLmPermissionHelper.hasManageAllFilesAccess();
    }

    public static boolean openManageAllFilesAccessSettings() {
        return BlueLmPermissionHelper.openManageAllFilesAccessSettings(UnityPlayer.currentActivity);
    }
}
