window.__invoke = function (methodName, argsJson) {
    try {
        // 从 window.expression 对象中获取方法
        var container = window.expression;
        if (!container || typeof container !== 'object') {
            return "Error: window.expression is not defined";
        }

        var fn = container[methodName];
        if (typeof fn !== "function") {
            return "Method not found: " + methodName;
        }

        // 解析JSON参数数组
        var args = JSON.parse(argsJson);
        if (!Array.isArray(args)) {
            args = [args];
        }

        // 展开数组作为参数调用函数
        return fn.apply(container, args);
    } catch (e) {
        return "Invoke error: " + e.message;
    }
};