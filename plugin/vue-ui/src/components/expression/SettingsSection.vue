<script setup>
import { computed } from 'vue';
import { useExpressionStore } from '@/store/useExpressionStore';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { NumberField, NumberFieldContent, NumberFieldDecrement, NumberFieldIncrement, NumberFieldInput } from '@/components/ui/number-field';
import { BracesIcon, CalculatorIcon, HashIcon, ListFilterIcon } from 'lucide-vue-next';

const props = defineProps({ service: { type: Object, required: true } });
const store = useExpressionStore();
const globalCalculateEnabled = computed({
  get: () => !!props.service.Properties.GlobalCalculateEnabled,
  set: (value) => props.service.setProperty('GlobalCalculateEnabled', !!value),
});
const decimalPrecision = computed({
  get: () => store.settings.decimalPrecision,
  set: (value) => {
    const number = Number(value);
    store.settings.decimalPrecision = Number.isFinite(number)
      ? Math.max(0, Math.min(28, Math.round(number)))
      : 0;
  },
});

const settingItems = [
  {
    key: 'global',
    title: '全局搜索直接计算',
    description: '开启后输入表达式直接计算，空框可计算剪贴板算式。',
    icon: CalculatorIcon,
  },
  {
    key: 'script',
    title: '自定义脚本',
    description: '计算表达式时加载 window.expression 中的函数。',
    icon: BracesIcon,
  },
  {
    key: 'separator',
    title: '千分位分隔符',
    description: '将 1000000 显示为 1,000,000。',
    icon: ListFilterIcon,
  },
];
</script>

<template>
  <div class="space-y-4 p-4">
    <section
      v-for="item in settingItems"
      :key="item.key"
      class="flex items-center gap-3 rounded-xl border bg-background/70 p-3 shadow-xs transition-colors hover:border-primary/20"
    >
      <div class="flex size-9 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
        <component :is="item.icon" class="size-4" />
      </div>
      <div class="min-w-0 flex-1">
        <Label :for="`setting-${item.key}`" class="text-sm font-medium">{{ item.title }}</Label>
        <p class="mt-0.5 text-xs leading-relaxed text-muted-foreground">{{ item.description }}</p>
      </div>
      <Switch
        v-if="item.key === 'global'"
        :id="`setting-${item.key}`"
        v-model="globalCalculateEnabled"
        :disabled="service._loading || !!service._error"
      />
      <Switch
        v-else-if="item.key === 'script'"
        :id="`setting-${item.key}`"
        v-model="store.settings.scriptEnabled"
      />
      <Switch
        v-else
        :id="`setting-${item.key}`"
        v-model="store.settings.useThousandsSeparator"
      />
    </section>

    <section class="rounded-xl border bg-background/70 p-3 shadow-xs transition-colors hover:border-primary/20">
      <div class="mb-3 flex items-start gap-3">
        <div class="flex size-9 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
          <HashIcon class="size-4" />
        </div>
        <div>
          <Label for="decimal-precision" class="text-sm font-medium">小数精度</Label>
          <p class="mt-0.5 text-xs leading-relaxed text-muted-foreground">小数最多保留 28 位，0 表示舍入到整数；浮点结果按自身精度显示。</p>
        </div>
      </div>
      <NumberField id="decimal-precision" v-model="decimalPrecision" :min="0" :max="28" :step="1">
        <NumberFieldContent class="bg-card">
          <NumberFieldDecrement />
          <NumberFieldInput />
          <NumberFieldIncrement />
        </NumberFieldContent>
      </NumberField>
    </section>

    <div
      class="flex items-start gap-2.5 rounded-lg border px-3 py-2.5 text-xs"
      :class="store.hasUnsavedChanges
        ? 'border-warning/30 bg-warning/10 text-amber-800 dark:text-amber-200'
        : 'border-border bg-muted/40 text-muted-foreground'"
    >
      <span class="mt-1 size-1.5 shrink-0 rounded-full" :class="store.hasUnsavedChanges ? 'bg-warning' : 'bg-emerald-500'" />
      {{ store.hasUnsavedChanges ? '设置已修改，点击顶部“应用更改”保存。' : '所有设置均已同步。' }}
    </div>
  </div>
</template>
