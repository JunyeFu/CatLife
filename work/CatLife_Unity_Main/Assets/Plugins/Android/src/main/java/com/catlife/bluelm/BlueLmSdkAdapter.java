package com.catlife.bluelm;

import java.lang.reflect.Constructor;
import java.lang.reflect.Field;
import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;

public final class BlueLmSdkAdapter {
    private Object manager;
    private boolean ready;

    public synchronized InitOutcome init(String modelPath) {
        try {
            Class<?> managerClass = findClass(
                "com.vivo.ai.llm.LlmManager",
                "com.vivo.llm.LlmManager",
                "com.vivo.bluelm.LlmManager",
                "LlmManager");
            if (managerClass == null) {
                ready = false;
                return InitOutcome.failure(BlueLmEngine.CODE_SDK_NOT_LINKED, "LLM_MANAGER_CLASS_MISSING");
            }

            Object resolvedManager = createManager(managerClass);
            if (resolvedManager == null) {
                ready = false;
                return InitOutcome.failure(BlueLmEngine.CODE_SDK_NOT_LINKED, "LLM_MANAGER_CREATE_FAILED");
            }

            Class<?> configClass = findClass(
                "com.vivo.ai.llm.LlmConfig",
                "com.vivo.llm.LlmConfig",
                "com.vivo.bluelm.LlmConfig",
                "LlmConfig");
            Object config = configClass != null ? create(configClass) : null;
            if (config != null) {
                setFieldIfPresent(config, "modelPath", modelPath);
                setFieldIfPresent(config, "multimodal", Boolean.FALSE);
                setFieldIfPresent(config, "nPredict", Integer.valueOf(256));
                setFieldIfPresent(config, "nCtx", Integer.valueOf(2048));
                setFieldIfPresent(config, "nThreads", Integer.valueOf(4));
                setFieldIfPresent(config, "npuPower", Integer.valueOf(80));
                setFieldIfPresent(config, "temperature", Float.valueOf(0.0f));
                setFieldIfPresent(config, "topP", Float.valueOf(1.0f));
                setFieldIfPresent(config, "topK", Integer.valueOf(1));
            }

            Object initResult = invokeInit(resolvedManager, config, modelPath);
            if (!isSuccessResult(initResult)) {
                ready = false;
                return InitOutcome.failure(BlueLmEngine.CODE_SDK_NOT_LINKED, "LLM_INIT_FAILED");
            }

            manager = resolvedManager;
            ready = true;
            return InitOutcome.success();
        } catch (Throwable throwable) {
            ready = false;
            return InitOutcome.failure(BlueLmEngine.CODE_SDK_NOT_LINKED, "LLM_INIT_" + throwable.getClass().getSimpleName());
        }
    }

    public synchronized boolean isReady() {
        return ready && manager != null;
    }

    public void generate(String prompt, GenerateOutcomeCallback callback) {
        Object safeManager;
        synchronized (this) {
            safeManager = manager;
        }

        if (safeManager == null || !isReady()) {
            if (callback != null) {
                callback.onComplete(GenerateOutcome.failure("LLM_NOT_READY"));
            }
            return;
        }

        try {
            Method method = findGenerateMethod(safeManager.getClass());
            if (method == null) {
                if (callback != null) {
                    callback.onComplete(GenerateOutcome.failure("LLM_GENERATE_METHOD_MISSING"));
                }
                return;
            }

            Class<?>[] params = method.getParameterTypes();
            if (params.length == 1) {
                Object result = method.invoke(safeManager, prompt);
                if (callback != null) {
                    callback.onComplete(GenerateOutcome.fromReturnValue(result));
                }
                return;
            }

            if (params.length >= 2) {
                Object tokenCallback = createTokenCallback(params[1], callback);
                if (tokenCallback == null) {
                    if (callback != null) {
                        callback.onComplete(GenerateOutcome.failure("TOKEN_CALLBACK_CREATE_FAILED"));
                    }
                    return;
                }

                Object result = method.invoke(safeManager, prompt, tokenCallback);
                if (result instanceof String && callback != null) {
                    callback.onComplete(GenerateOutcome.success((String)result));
                }
            }
        } catch (Throwable throwable) {
            if (callback != null) {
                callback.onComplete(GenerateOutcome.failure("LLM_GENERATE_" + throwable.getClass().getSimpleName()));
            }
        }
    }

    private static Class<?> findClass(String... classNames) {
        for (int i = 0; i < classNames.length; i++) {
            try {
                return Class.forName(classNames[i]);
            } catch (Throwable ignored) {
            }
        }

        return null;
    }

    private static Object createManager(Class<?> managerClass) throws Exception {
        Method[] methods = managerClass.getMethods();
        for (int i = 0; i < methods.length; i++) {
            Method method = methods[i];
            String name = method.getName();
            if ((name.equals("getInstance") || name.equals("getDefault")) && method.getParameterTypes().length == 0) {
                Object value = method.invoke(null);
                if (value != null) {
                    return value;
                }
            }
        }

        return create(managerClass);
    }

    private static Object create(Class<?> clazz) throws Exception {
        Constructor<?> constructor = clazz.getDeclaredConstructor();
        constructor.setAccessible(true);
        return constructor.newInstance();
    }

    private static void setFieldIfPresent(Object target, String name, Object value) {
        try {
            Field field = target.getClass().getField(name);
            field.setAccessible(true);
            Class<?> type = field.getType();
            if (type == int.class || type == Integer.class) {
                field.set(target, Integer.valueOf(((Number)value).intValue()));
            } else if (type == float.class || type == Float.class) {
                field.set(target, Float.valueOf(((Number)value).floatValue()));
            } else if (type == boolean.class || type == Boolean.class) {
                field.set(target, value);
            } else {
                field.set(target, value == null ? "" : value.toString());
            }
        } catch (Throwable ignored) {
        }
    }

    private static Object invokeInit(Object manager, Object config, String modelPath) throws Exception {
        Method[] methods = manager.getClass().getMethods();
        for (int i = 0; i < methods.length; i++) {
            Method method = methods[i];
            if (!method.getName().equals("init")) {
                continue;
            }

            Class<?>[] params = method.getParameterTypes();
            if (params.length == 1 && config != null && params[0].isAssignableFrom(config.getClass())) {
                return method.invoke(manager, config);
            }

            if (params.length == 1 && params[0] == String.class) {
                return method.invoke(manager, modelPath);
            }

            if (params.length == 0) {
                return method.invoke(manager);
            }
        }

        return Boolean.FALSE;
    }

    private static boolean isSuccessResult(Object result) {
        if (result == null) {
            return true;
        }

        if (result instanceof Boolean) {
            return ((Boolean)result).booleanValue();
        }

        if (result instanceof Number) {
            return ((Number)result).intValue() == 0;
        }

        String text = String.valueOf(result).toLowerCase();
        return text.contains("ok") || text.contains("success") || text.equals("0") || text.equals("true");
    }

    private static Method findGenerateMethod(Class<?> managerClass) {
        Method[] methods = managerClass.getMethods();
        for (int i = 0; i < methods.length; i++) {
            Method method = methods[i];
            if (!method.getName().equals("generate")) {
                continue;
            }

            Class<?>[] params = method.getParameterTypes();
            if (params.length >= 1 && params[0] == String.class) {
                return method;
            }
        }

        return null;
    }

    private static Object createTokenCallback(Class<?> callbackType, final GenerateOutcomeCallback callback) {
        if (!callbackType.isInterface()) {
            return null;
        }

        final StringBuilder tokens = new StringBuilder();
        InvocationHandler handler = new InvocationHandler() {
            private boolean completed;

            @Override
            public Object invoke(Object proxy, Method method, Object[] args) {
                String name = method.getName().toLowerCase();
                if (name.contains("token")) {
                    appendFirstString(tokens, args);
                    return null;
                }

                if (name.contains("complete") || name.contains("finish") || name.contains("done")) {
                    appendFirstString(tokens, args);
                    if (!completed && callback != null) {
                        completed = true;
                        callback.onComplete(GenerateOutcome.success(tokens.toString()));
                    }
                    return null;
                }

                if (name.contains("error") || name.contains("fail")) {
                    if (!completed && callback != null) {
                        completed = true;
                        callback.onComplete(GenerateOutcome.failure(firstString(args, "LLM_GENERATE_ERROR")));
                    }
                    return null;
                }

                return null;
            }
        };

        return Proxy.newProxyInstance(callbackType.getClassLoader(), new Class<?>[] { callbackType }, handler);
    }

    private static void appendFirstString(StringBuilder sb, Object[] args) {
        String value = firstString(args, "");
        if (value.length() > 0) {
            sb.append(value);
        }
    }

    private static String firstString(Object[] args, String fallback) {
        if (args == null) {
            return fallback;
        }

        for (int i = 0; i < args.length; i++) {
            if (args[i] != null) {
                return String.valueOf(args[i]);
            }
        }

        return fallback;
    }

    public interface GenerateOutcomeCallback {
        void onComplete(GenerateOutcome outcome);
    }

    public static final class InitOutcome {
        public final boolean ok;
        public final int code;
        public final String message;

        private InitOutcome(boolean ok, int code, String message) {
            this.ok = ok;
            this.code = code;
            this.message = message == null ? "" : message;
        }

        public static InitOutcome success() {
            return new InitOutcome(true, BlueLmEngine.CODE_OK, "OK");
        }

        public static InitOutcome failure(int code, String message) {
            return new InitOutcome(false, code, message);
        }
    }

    public static final class GenerateOutcome {
        public final boolean ok;
        public final String text;
        public final String error;

        private GenerateOutcome(boolean ok, String text, String error) {
            this.ok = ok;
            this.text = text == null ? "" : text;
            this.error = error == null ? "" : error;
        }

        public static GenerateOutcome success(String text) {
            return new GenerateOutcome(true, text, "");
        }

        public static GenerateOutcome failure(String error) {
            return new GenerateOutcome(false, "", error);
        }

        public static GenerateOutcome fromReturnValue(Object value) {
            if (value == null) {
                return failure("LLM_GENERATE_EMPTY_RETURN");
            }

            return success(String.valueOf(value));
        }
    }
}
