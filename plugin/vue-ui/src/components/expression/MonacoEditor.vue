<script setup>
import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue';
import { loadMonaco } from '@/lib/loadMonaco';
import { useThemeStore } from '@/store/useThemeStore';
import { Skeleton } from '@/components/ui/skeleton';

const props = defineProps({
  modelValue: { type: String, default: '' },
  readOnly: { type: Boolean, default: false },
  language: { type: String, default: 'javascript' },
});

const emit = defineEmits(['update:modelValue', 'ready']);
const themeStore = useThemeStore();
const containerRef = ref(null);
const isLoading = ref(true);
const loadError = ref(null);

let editor = null;
let monaco = null;
let customThemeDefined = false;
const DARK_THEME = 'miokit-dark';

function defineDarkTheme() {
  if (!monaco || customThemeDefined) return;
  monaco.editor.defineTheme(DARK_THEME, {
    base: 'vs-dark',
    inherit: true,
    rules: [],
    colors: {
      'editor.background': '#020713',
      'editor.lineHighlightBackground': '#0a1628',
      'editorGutter.background': '#020713',
      'editorWidget.background': '#0a1628',
      'dropdown.background': '#0a1628',
      'input.background': '#0a1628',
      'minimap.background': '#020713',
    },
  });
  customThemeDefined = true;
}

function getTheme() {
  return themeStore.theme === 'dark' ? DARK_THEME : 'vs';
}

function handleThemeChange(event) {
  if (monaco && editor) {
    monaco.editor.setTheme(event.detail?.theme === 'dark' ? DARK_THEME : 'vs');
  }
}

async function initEditor() {
  if (!containerRef.value) return;
  try {
    isLoading.value = true;
    loadError.value = null;
    monaco = await loadMonaco();
    defineDarkTheme();
    await nextTick();
    if (!containerRef.value) return;

    editor = monaco.editor.create(containerRef.value, {
      value: props.modelValue,
      language: props.language,
      theme: getTheme(),
      readOnly: props.readOnly,
      automaticLayout: true,
      minimap: { enabled: false },
      scrollBeyondLastLine: false,
      fontSize: 14,
      fontFamily: "'Cascadia Code', 'JetBrains Mono', Consolas, monospace",
      fontLigatures: true,
      lineHeight: 22,
      lineNumbers: 'on',
      wordWrap: 'on',
      tabSize: 2,
      insertSpaces: true,
      folding: true,
      glyphMargin: false,
      renderLineHighlight: 'line',
      renderWhitespace: 'selection',
      smoothScrolling: true,
      cursorBlinking: 'smooth',
      padding: { top: 12, bottom: 12 },
      bracketPairColorization: { enabled: true },
      guides: { bracketPairs: true, indentation: true },
      stickyScroll: { enabled: false },
      scrollbar: { verticalScrollbarSize: 10, horizontalScrollbarSize: 10 },
    });
    editor.onDidChangeModelContent(() => emit('update:modelValue', editor.getValue()));
    isLoading.value = false;
    emit('ready');
  } catch (error) {
    loadError.value = error?.message || '加载编辑器失败';
    isLoading.value = false;
  }
}

watch(() => props.modelValue, (value) => {
  if (editor && editor.getValue() !== value) editor.setValue(value || '');
});
watch(() => props.readOnly, (value) => editor?.updateOptions({ readOnly: value }));
watch(() => props.language, (value) => {
  const model = editor?.getModel();
  if (model && monaco) monaco.editor.setModelLanguage(model, value);
});

onMounted(() => {
  window.addEventListener('theme-changed', handleThemeChange);
  initEditor();
});

onUnmounted(() => {
  window.removeEventListener('theme-changed', handleThemeChange);
  editor?.dispose();
  editor = null;
});
</script>

<template>
  <div class="relative h-full w-full">
    <div v-if="isLoading" class="absolute inset-0 flex flex-col gap-2 bg-background p-5">
      <Skeleton class="h-4 w-3/4" />
      <Skeleton class="h-4 w-1/2" />
      <Skeleton class="h-4 w-5/6" />
      <Skeleton class="h-4 w-2/3" />
      <Skeleton class="h-4 w-4/5" />
      <Skeleton class="h-4 w-1/3" />
    </div>
    <div v-else-if="loadError" class="absolute inset-0 flex flex-col items-center justify-center gap-4 p-4">
      <p class="text-sm text-destructive">{{ loadError }}</p>
      <button class="rounded-md bg-primary px-4 py-2 text-sm text-primary-foreground" @click="initEditor">重试</button>
    </div>
    <div ref="containerRef" class="h-full w-full" :class="{ invisible: isLoading || loadError }" />
  </div>
</template>
