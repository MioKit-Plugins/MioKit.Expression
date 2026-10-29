<script setup>
import { computed, ref, watch } from 'vue';
import { useExpressionStore } from '@/store/useExpressionStore';
import { useBridgeService, useBridgeServiceEvent } from '@/composables/useDotnetBridge';
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/components/ui/resizable';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  BuiltInScriptDialog,
  EditorToolbar,
  LogSection,
  MonacoEditor,
  SettingsSection,
  TestSection,
} from '@/components/expression';
import { FlaskConicalIcon, SettingsIcon } from 'lucide-vue-next';

const service = useBridgeService('ExpressionService');
const store = useExpressionStore();
const activeTab = ref('test');
const showReference = ref(false);
const initialized = ref(false);
const serviceReady = computed(() => !service._loading && !service._error && !!service._service);
const lineCount = computed(() => store.code ? store.code.split(/\r?\n/).length : 1);
const characterCount = computed(() => store.code.length);

store.bindService(service);
useBridgeServiceEvent(service, 'LogEmitted', store.addBridgeLog);

watch(serviceReady, async (ready) => {
  if (!ready || initialized.value) return;
  initialized.value = true;
  await store.loadInitialData();
}, { immediate: true });
</script>

<template>
  <div class="expression-workbench flex h-screen flex-col overflow-hidden bg-muted/20 p-2">
    <div v-if="service._error" class="m-auto max-w-lg rounded-xl border border-destructive/30 bg-card p-6 text-center shadow-sm">
      <div class="mx-auto mb-3 flex size-10 items-center justify-center rounded-full bg-destructive/10 text-destructive">!</div>
      <h1 class="font-semibold">表达式服务加载失败</h1>
      <p class="mt-2 text-sm text-muted-foreground">{{ service._error }}</p>
    </div>
    <div v-else-if="service._loading || store.isLoading" class="m-auto flex flex-col items-center gap-3 text-sm text-muted-foreground">
      <div class="size-7 animate-spin rounded-full border-2 border-primary/20 border-t-primary" />
      <span>正在加载表达式编辑器…</span>
    </div>
    <ResizablePanelGroup v-else direction="horizontal" class="min-h-0 flex-1 overflow-hidden rounded-xl border bg-card shadow-sm">
      <ResizablePanel :default-size="70" :min-size="40">
        <section class="flex h-full flex-col bg-background">
          <EditorToolbar @view-reference="showReference = true" />
          <div class="min-h-0 flex-1 bg-background">
            <MonacoEditor :model-value="store.code" @update:model-value="store.setCode" />
          </div>
          <footer class="flex h-7 shrink-0 items-center justify-between border-t bg-card px-3 text-[11px] text-muted-foreground">
            <div class="flex items-center gap-3">
              <span class="inline-flex items-center gap-1.5">
                <span class="size-1.5 rounded-full bg-emerald-500" />
                ExpressionService 已连接
              </span>
              <span>JavaScript</span>
            </div>
            <div class="flex items-center gap-3 font-mono">
              <span>{{ lineCount }} 行</span>
              <span>{{ characterCount }} 字符</span>
              <span>UTF-8</span>
            </div>
          </footer>
        </section>
      </ResizablePanel>

      <ResizableHandle with-handle />

      <ResizablePanel :default-size="30" :min-size="20">
        <aside class="h-full bg-card">
          <Tabs v-model="activeTab" class="flex h-full flex-col">
          <div class="border-b px-3 py-3">
            <div class="mb-2.5">
              <h2 class="text-sm font-semibold">检查与测试</h2>
              <p class="mt-0.5 text-xs text-muted-foreground">验证脚本输出并调整运行选项</p>
            </div>
            <TabsList class="grid h-9 w-full grid-cols-2 bg-muted/70">
              <TabsTrigger value="test"><FlaskConicalIcon class="size-4" />脚本测试</TabsTrigger>
              <TabsTrigger value="settings"><SettingsIcon class="size-4" />全局设置</TabsTrigger>
            </TabsList>
          </div>
          <TabsContent value="test" class="mt-0 min-h-0 flex-1 overflow-hidden">
            <div class="flex h-full flex-col">
              <div class="max-h-[55%] shrink-0 overflow-y-auto border-b"><TestSection /></div>
              <div class="min-h-0 flex-1"><LogSection /></div>
            </div>
          </TabsContent>
          <TabsContent value="settings" class="mt-0 min-h-0 flex-1 overflow-auto">
            <SettingsSection :service="service" />
          </TabsContent>
          </Tabs>
        </aside>
      </ResizablePanel>
    </ResizablePanelGroup>
    <BuiltInScriptDialog v-model:open="showReference" />
  </div>
</template>
