<script setup>
import { useExpressionStore } from '@/store/useExpressionStore';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { BracesIcon, CheckCircle2Icon, FileCodeIcon, RefreshCwIcon, SaveIcon } from 'lucide-vue-next';

const emit = defineEmits(['view-reference']);
const store = useExpressionStore();
</script>

<template>
  <TooltipProvider>
    <header class="flex min-h-16 items-center justify-between gap-4 border-b bg-card/95 px-4 py-2 backdrop-blur">
      <div class="flex min-w-0 items-center gap-3">
        <div class="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary ring-1 ring-primary/15">
          <BracesIcon class="size-5" />
        </div>
        <div class="min-w-0">
          <div class="flex items-center gap-2">
            <h1 class="truncate text-sm font-semibold tracking-tight">表达式脚本</h1>
            <span
              class="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[11px] font-medium"
              :class="store.hasUnsavedChanges
                ? 'bg-warning/15 text-amber-700 dark:text-amber-300'
                : 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300'"
            >
              <span
                class="size-1.5 rounded-full"
                :class="store.hasUnsavedChanges ? 'bg-warning' : 'bg-emerald-500'"
              />
              {{ store.hasUnsavedChanges ? '有未保存更改' : '已同步' }}
            </span>
          </div>
          <p class="mt-0.5 truncate text-xs text-muted-foreground">JavaScript · window.expression</p>
        </div>
      </div>

      <div class="flex shrink-0 items-center gap-1.5">
        <Tooltip>
          <TooltipTrigger as-child>
            <Button variant="ghost" size="sm" @click="emit('view-reference')">
              <FileCodeIcon class="size-4" />
              <span class="hidden xl:inline">脚本参考</span>
            </Button>
          </TooltipTrigger>
          <TooltipContent>查看内置函数和脚本结构</TooltipContent>
        </Tooltip>

        <div class="mx-1 h-5 w-px bg-border" />

        <Tooltip>
          <TooltipTrigger as-child>
            <Button variant="outline" size="sm" :disabled="store.isCompiling || store.isLoading" @click="store.compileScript">
              <Spinner v-if="store.isCompiling" />
              <RefreshCwIcon v-else class="size-4" />
              编译
            </Button>
          </TooltipTrigger>
          <TooltipContent>验证脚本并刷新可用函数</TooltipContent>
        </Tooltip>

        <Tooltip>
          <TooltipTrigger as-child>
            <Button
              size="sm"
              :disabled="store.isApplying || store.isLoading || !store.hasUnsavedChanges"
              @click="store.applyScript"
            >
              <Spinner v-if="store.isApplying" />
              <SaveIcon v-else-if="store.hasUnsavedChanges" class="size-4" />
              <CheckCircle2Icon v-else class="size-4" />
              {{ store.isApplying ? '保存中' : store.hasUnsavedChanges ? '应用更改' : '已保存' }}
            </Button>
          </TooltipTrigger>
          <TooltipContent>{{ store.hasUnsavedChanges ? '保存脚本和全局设置' : '当前内容已保存' }}</TooltipContent>
        </Tooltip>
      </div>
    </header>
  </TooltipProvider>
</template>
