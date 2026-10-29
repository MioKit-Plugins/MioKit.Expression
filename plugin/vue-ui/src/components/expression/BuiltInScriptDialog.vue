<script setup>
import { computed } from 'vue';
import { useExpressionStore } from '@/store/useExpressionStore';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Badge } from '@/components/ui/badge';
import { BracesIcon } from 'lucide-vue-next';

const props = defineProps({ open: { type: Boolean, default: false } });
const emit = defineEmits(['update:open']);
const store = useExpressionStore();
const isOpen = computed({
  get: () => props.open,
  set: (value) => emit('update:open', value),
});
</script>

<template>
  <Dialog v-model:open="isOpen">
    <DialogContent class="flex h-[80vh] !w-[90vw] !max-w-5xl flex-col gap-3 overflow-hidden p-0">
      <DialogHeader class="border-b px-5 py-4">
        <DialogTitle class="flex items-center gap-2 text-base">
          <span class="flex size-8 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <BracesIcon class="size-4" />
          </span>
          内置脚本参考
          <Badge variant="secondary" class="ml-1 font-mono text-[10px]">JavaScript</Badge>
        </DialogTitle>
      </DialogHeader>
      <p class="px-5 text-xs text-muted-foreground">自定义函数请挂载到 <code class="rounded bg-muted px-1.5 py-0.5 font-mono text-foreground">window.expression</code>，随后可在表达式中直接调用。</p>
      <ScrollArea class="mx-5 mb-5 min-h-0 flex-1 overflow-hidden rounded-lg border bg-muted/40">
        <pre class="overflow-x-auto p-4 text-xs leading-5 text-foreground/90"><code>{{ store.builtInScript }}</code></pre>
      </ScrollArea>
    </DialogContent>
  </Dialog>
</template>
