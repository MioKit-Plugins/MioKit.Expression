import { defineStore } from 'pinia';
import { computed, reactive, ref } from 'vue';
import { toast } from 'vue-sonner';

function pick(value, camelName, pascalName) {
  return value?.[camelName] ?? value?.[pascalName];
}

export const useExpressionStore = defineStore('expression', () => {
  const code = ref('');
  const builtInScript = ref('');
  const availableFunctions = ref([]);
  const logs = ref([]);
  const testResult = ref(null);
  const isLoading = ref(false);
  const isCompiling = ref(false);
  const isApplying = ref(false);
  const isCalculating = ref(false);
  const savedSnapshot = ref('');
  const settings = reactive({
    decimalPrecision: 10,
    useThousandsSeparator: false,
    scriptEnabled: true,
  });

  let serviceStore = null;
  let logId = 0;

  function createSnapshot() {
    return JSON.stringify({
      code: code.value,
      decimalPrecision: Number(settings.decimalPrecision),
      useThousandsSeparator: !!settings.useThousandsSeparator,
      scriptEnabled: !!settings.scriptEnabled,
    });
  }

  const hasUnsavedChanges = computed(() => savedSnapshot.value !== createSnapshot());
  const isBusy = computed(() => (
    isLoading.value || isCompiling.value || isApplying.value || isCalculating.value
  ));

  function bindService(service) {
    serviceStore = service;
  }

  function requireMethods() {
    if (!serviceStore || serviceStore._loading || serviceStore._error) {
      throw new Error(serviceStore?._error || '表达式服务尚未就绪');
    }
    return serviceStore.Methods;
  }

  async function loadInitialData() {
    isLoading.value = true;
    try {
      const methods = requireMethods();
      const [savedCode, loadedSettings, referenceScript] = await Promise.all([
        methods.GetSavedScript(),
        methods.GetSettings(),
        methods.GetBuiltInScript(),
      ]);

      code.value = savedCode || '';
      builtInScript.value = referenceScript || '';
      settings.decimalPrecision = pick(loadedSettings, 'decimalPrecision', 'DecimalPrecision') ?? 10;
      settings.useThousandsSeparator = !!pick(
        loadedSettings,
        'useThousandsSeparator',
        'UseThousandsSeparator',
      );
      settings.scriptEnabled = !!pick(loadedSettings, 'scriptEnabled', 'ScriptEnabled');
      savedSnapshot.value = createSnapshot();
    } catch (error) {
      addLog('error', `加载设置失败：${error?.message ?? String(error)}`);
      toast.error('加载设置失败', { description: error?.message ?? String(error) });
    } finally {
      isLoading.value = false;
    }
  }

  function setCode(value) {
    code.value = value || '';
  }

  function addLog(level, message, timestamp = null) {
    const date = timestamp ? new Date(timestamp) : new Date();
    logs.value.push({
      id: ++logId,
      timestamp: Number.isNaN(date.getTime()) ? new Date().toLocaleTimeString() : date.toLocaleTimeString(),
      level: level || 'info',
      message: message || '',
    });
  }

  function addBridgeLog(payload) {
    addLog(
      pick(payload, 'level', 'Level') || 'info',
      pick(payload, 'message', 'Message') || '',
      pick(payload, 'timestamp', 'Timestamp'),
    );
  }

  function clearLogs() {
    logs.value = [];
  }

  async function compileScript() {
    if (isCompiling.value) return null;
    isCompiling.value = true;
    try {
      const result = await requireMethods().CompileScript(code.value);
      availableFunctions.value = pick(result, 'functions', 'Functions') ?? [];
      if (pick(result, 'success', 'Success')) {
        toast.success(pick(result, 'message', 'Message') || '脚本编译成功');
      } else {
        toast.error('编译失败', { description: pick(result, 'message', 'Message') || '脚本编译失败' });
      }
      return result;
    } catch (error) {
      addLog('error', error?.message ?? String(error));
      toast.error('编译失败', { description: error?.message ?? String(error) });
      return null;
    } finally {
      isCompiling.value = false;
    }
  }

  async function applyScript() {
    if (isApplying.value) return null;
    isApplying.value = true;
    try {
      const result = await requireMethods().ApplyScript(
        code.value,
        Number(settings.decimalPrecision),
        !!settings.useThousandsSeparator,
        !!settings.scriptEnabled,
      );
      if (pick(result, 'success', 'Success')) {
        savedSnapshot.value = createSnapshot();
        toast.success(pick(result, 'message', 'Message') || '设置已保存');
        addLog('info', '脚本和设置已应用');
      } else {
        toast.error('应用失败', { description: pick(result, 'message', 'Message') || '设置保存失败' });
      }
      return result;
    } catch (error) {
      addLog('error', error?.message ?? String(error));
      toast.error('应用失败', { description: error?.message ?? String(error) });
      return null;
    } finally {
      isApplying.value = false;
    }
  }

  async function calculateExpression(expression) {
    const source = expression?.trim();
    if (!source || isCalculating.value) return null;
    isCalculating.value = true;
    try {
      const result = await requireMethods().Calculate(source);
      testResult.value = {
        expression: source,
        success: !!pick(result, 'success', 'Success'),
        result: pick(result, 'result', 'Result') || '',
        error: pick(result, 'error', 'Error') || '',
      };
      return result;
    } catch (error) {
      const message = error?.message ?? String(error);
      testResult.value = { expression: source, success: false, result: '', error: message };
      addLog('error', message);
      return null;
    } finally {
      isCalculating.value = false;
    }
  }

  return {
    code,
    builtInScript,
    availableFunctions,
    logs,
    testResult,
    settings,
    isLoading,
    isCompiling,
    isApplying,
    isCalculating,
    hasUnsavedChanges,
    isBusy,
    bindService,
    loadInitialData,
    setCode,
    addBridgeLog,
    clearLogs,
    compileScript,
    applyScript,
    calculateExpression,
  };
});
