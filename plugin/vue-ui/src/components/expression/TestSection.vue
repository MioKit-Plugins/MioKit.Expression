<script setup>
import { computed, ref } from 'vue';
import { useExpressionStore } from '@/store/useExpressionStore';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { CheckCircle2Icon, FlaskConicalIcon, PlayIcon, XCircleIcon } from 'lucide-vue-next';

const store = useExpressionStore();
const expression = ref('1 + 2 * 3');
const testResult = computed(() => store.testResult);

function calculate() {
  return store.calculateExpression(expression.value);
}

function selectFunction(name) {
  expression.value = `${name}()`;
}
</script>

<template>
  <div class="space-y-4 p-4">
    <section class="rounded-xl border bg-background/70 p-3 shadow-xs">
      <div class="mb-3 flex items-center gap-2">
        <div class="flex size-7 items-center justify-center rounded-md bg-primary/10 text-primary">
          <FlaskConicalIcon class="size-4" />
        </div>
        <div>
          <h3 class="text-sm font-medium">快速计算</h3>
          <p class="text-[11px] text-muted-foreground">使用当前已应用的脚本执行</p>
        </div>
      </div>
      <div class="flex gap-2">
        <Input
          id="expression-input"
          v-model="expression"
          class="h-9 flex-1 bg-card font-mono text-sm"
          placeholder="1 + 2 * 3"
          autocomplete="off"
          @keyup.enter="calculate"
        />
        <Button size="sm" class="h-9" :disabled="!expression.trim() || store.isCalculating" @click="calculate">
          <span v-if="store.isCalculating" class="size-3.5 animate-spin rounded-full border-2 border-primary-foreground/30 border-t-primary-foreground" />
          <PlayIcon v-else class="size-3.5" />
          {{ store.isCalculating ? '计算中' : '运行' }}
        </Button>
      </div>
    </section>

    <section v-if="testResult" class="overflow-hidden rounded-xl border bg-background/70 shadow-xs">
      <div class="flex items-center gap-2 border-b px-3 py-2 text-xs font-medium">
        <CheckCircle2Icon v-if="testResult.success" class="size-4 text-emerald-500" />
        <XCircleIcon v-else class="size-4 text-destructive" />
        {{ testResult.success ? '计算成功' : '计算失败' }}
      </div>
      <div
        class="max-h-28 overflow-y-auto p-3 font-mono text-sm"
        :class="testResult.success ? 'text-foreground' : 'text-destructive'"
      >
        <span v-if="testResult.success" class="break-all text-base font-semibold">{{ testResult.result }}</span>
        <span v-else class="break-all">{{ testResult.error }}</span>
      </div>
    </section>

    <section class="space-y-2.5">
      <div class="flex items-center justify-between">
        <h3 class="text-xs font-medium text-muted-foreground">可用函数</h3>
        <span class="rounded-full bg-muted px-2 py-0.5 text-[10px] text-muted-foreground">
          {{ store.availableFunctions.length }} 个
        </span>
      </div>
      <div v-if="store.availableFunctions.length" class="flex max-h-24 flex-wrap gap-1.5 overflow-y-auto">
        <Badge
          v-for="name in store.availableFunctions"
          :key="name"
          variant="outline"
          class="cursor-pointer rounded-md bg-background/70 font-mono text-[11px] font-normal transition-colors hover:border-primary/40 hover:bg-primary/5 hover:text-primary"
          @click="selectFunction(name)"
        >
          {{ name }}()
        </Badge>
      </div>
      <div v-else class="rounded-lg border border-dashed px-3 py-4 text-center text-xs text-muted-foreground">
        编译脚本后，可用函数会显示在这里
      </div>
    </section>
  </div>
</template>
