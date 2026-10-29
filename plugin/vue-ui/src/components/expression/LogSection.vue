<script setup>
import { nextTick, ref, watch } from 'vue';
import { useExpressionStore } from '@/store/useExpressionStore';
import { Button } from '@/components/ui/button';
import { TerminalIcon, Trash2Icon } from 'lucide-vue-next';

const store = useExpressionStore();
const containerRef = ref(null);

const levelMeta = {
  info: { label: 'INFO', dot: 'bg-blue-500', text: 'text-blue-600 dark:text-blue-300' },
  warning: { label: 'WARN', dot: 'bg-warning', text: 'text-amber-700 dark:text-amber-300' },
  error: { label: 'ERROR', dot: 'bg-destructive', text: 'text-destructive' },
  debug: { label: 'DEBUG', dot: 'bg-muted-foreground', text: 'text-muted-foreground' },
};

function getLevel(level) {
  return levelMeta[level] || levelMeta.info;
}

watch(() => store.logs.length, async () => {
  await nextTick();
  if (containerRef.value) containerRef.value.scrollTop = containerRef.value.scrollHeight;
});
</script>

<template>
  <div class="flex h-full flex-col overflow-hidden bg-background/35">
    <div class="flex h-10 shrink-0 items-center justify-between border-b bg-card/80 px-3">
      <div class="flex items-center gap-2">
        <TerminalIcon class="size-3.5 text-muted-foreground" />
        <span class="text-xs font-medium">运行日志</span>
        <span class="rounded-full bg-muted px-1.5 py-0.5 text-[10px] tabular-nums text-muted-foreground">
          {{ store.logs.length }}
        </span>
      </div>
      <Button
        variant="ghost"
        size="icon-sm"
        class="size-7 text-muted-foreground"
        :disabled="!store.logs.length"
        title="清空日志"
        @click="store.clearLogs"
      >
        <Trash2Icon class="size-3.5" />
      </Button>
    </div>

    <div ref="containerRef" class="flex-1 overflow-y-auto p-2 font-mono">
      <div
        v-for="log in store.logs"
        :key="log.id"
        class="group grid grid-cols-[auto_42px_1fr] items-start gap-2 rounded-md px-2 py-1.5 text-[11px] transition-colors hover:bg-muted/60"
      >
        <span class="select-none text-muted-foreground/70">{{ log.timestamp }}</span>
        <span class="inline-flex items-center gap-1.5 font-semibold" :class="getLevel(log.level).text">
          <span class="size-1.5 rounded-full" :class="getLevel(log.level).dot" />
          {{ getLevel(log.level).label }}
        </span>
        <span class="break-all leading-relaxed text-foreground/85">{{ log.message }}</span>
      </div>

      <div v-if="!store.logs.length" class="flex h-full min-h-24 flex-col items-center justify-center gap-2 text-muted-foreground">
        <div class="flex size-9 items-center justify-center rounded-full border border-dashed">
          <TerminalIcon class="size-4" />
        </div>
        <span class="text-xs">运行脚本后，日志会显示在这里</span>
      </div>
    </div>
  </div>
</template>
