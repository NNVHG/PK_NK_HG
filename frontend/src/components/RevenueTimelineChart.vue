<template>
  <div class="revenue-timeline">
    <p v-if="empty" class="timeline-empty">Chưa ghi nhận giao dịch hoặc khoản hoàn trong kỳ này.</p>
    <template v-else>
      <div class="timeline-caption">
        <span>{{ periods.length }} mốc thời gian</span>
        <span><i class="timeline-key" aria-hidden="true"></i> Doanh thu dương</span>
        <span v-if="scale.min < 0"><i class="timeline-key timeline-key--negative" aria-hidden="true"></i> Doanh thu âm</span>
      </div>
      <div class="timeline-scroll" role="region" aria-label="Doanh thu thuần theo thời gian" tabindex="0">
        <div class="timeline-axis" aria-hidden="true">
          <div class="axis-labels">
            <span v-if="scale.min < 0" class="axis-start">{{ formatRevenueTick(scale.min) }}</span>
            <span class="axis-zero" :style="{ left: scale.zero + '%' }" :class="{ 'axis-zero--start': scale.zero === 0, 'axis-zero--end': scale.zero === 100 }">0</span>
            <span v-if="scale.max > 0" class="axis-end">{{ formatRevenueTick(scale.max) }}</span>
          </div>
        </div>
        <ul class="timeline-list" role="list">
          <li v-for="period in rows" :key="period.label" class="timeline-row">
            <span class="period-label">{{ period.displayLabel }}</span>
            <div class="period-track" aria-hidden="true">
              <span class="period-zero" :style="{ left: scale.zero + '%' }"></span>
              <span
                v-if="period.revenue !== 0" class="period-bar" :class="{ 'period-bar--negative': period.revenue < 0 }"
                :style="{ left: period.bar.start + '%', width: period.bar.size + '%' }"
              ></span>
            </div>
            <span class="period-metrics">
              <strong :class="{ 'amount-negative': period.revenue < 0 }">{{ period.amount }}</strong>
              <small>{{ period.transactionCount }} giao dịch</small>
            </span>
          </li>
        </ul>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { RevenueReport } from '@/services/reports';
import { createRevenueScale, formatRevenue, formatRevenueTick } from '@/utils/revenueChart';

const props = defineProps<{ periods: RevenueReport['revenueTimeline']; groupBy: string }>();
const scale = computed(() => createRevenueScale(props.periods.map(period => period.revenue)));
const empty = computed(() => props.periods.every(period => period.revenue === 0 && period.transactionCount === 0));

function periodLabel(label: string) {
  if (props.groupBy === 'Day' && /^\d{4}-\d{2}-\d{2}$/.test(label)) return label.split('-').reverse().join('/');
  if (props.groupBy === 'Month' && /^\d{4}-\d{2}$/.test(label)) return label.split('-').reverse().join('/');
  const week = /^(\d{4})-W(\d{2})$/.exec(label);
  return week ? `Tuần ${week[2]}/${week[1]}` : label;
}

const rows = computed(() => props.periods.map(period => ({
  ...period,
  displayLabel: periodLabel(period.label),
  amount: formatRevenue(period.revenue),
  bar: scale.value.bar(period.revenue),
})));
</script>

<style scoped>
.revenue-timeline {
  --revenue-fill: #147d78;
  --refund-fill: #b94d38;
  --timeline-columns: minmax(6.5rem, 1fr) minmax(0, 3fr) minmax(9rem, 1.4fr);
  container-type: inline-size;
  min-width: 0;
  color: #334155;
}
.timeline-caption { display: flex; flex-wrap: wrap; gap: .35rem .9rem; margin-bottom: .65rem; color: #475569; font-size: .78rem; }
.timeline-caption span { display: inline-flex; align-items: center; gap: .35rem; }
.timeline-key { width: .65rem; height: .45rem; border-radius: 2px; background: var(--revenue-fill); }
.timeline-key--negative { background: var(--refund-fill); }
.timeline-scroll { max-height: 27rem; overflow-y: auto; border: 1px solid #e0e8ef; border-radius: 8px; }
.timeline-scroll:focus-visible { outline: 2px solid #194a78; outline-offset: 2px; }
.timeline-axis, .timeline-row { display: grid; grid-template-columns: var(--timeline-columns); align-items: center; gap: .6rem 1rem; padding-inline: .8rem; }
.timeline-axis {
  position: sticky;
  inset-block-start: 0;
  z-index: 1;
  min-height: 1.8rem;
  background: #f6f8fb;
  border-bottom: 1px solid #e0e8ef;
}
.axis-labels { position: relative; grid-column: 2; height: 1.15rem; font-size: .72rem; color: #475569; font-variant-numeric: tabular-nums; }
.axis-labels > span { position: absolute; white-space: nowrap; }
.axis-start { left: 0; }
.axis-end { right: 0; }
.axis-zero { transform: translateX(-50%); }
.axis-zero--start { transform: none; }
.axis-zero--end { transform: translateX(-100%); }
.timeline-list { display: grid; padding: 0; margin: 0; list-style: none; }
.timeline-row { padding-block: .45rem; border-bottom: 1px solid #e8edf2; font-size: .84rem; }
.timeline-row:last-child { border-bottom: 0; }
.timeline-row:nth-child(even) { background: #f9fbfc; }
.period-label, .period-metrics { min-width: 0; overflow-wrap: anywhere; font-variant-numeric: tabular-nums; }
.period-label { font-weight: 700; }
.period-metrics { display: grid; gap: .1rem; text-align: end; color: #17334a; }
.period-metrics small { font-size: .72rem; color: #52667a; }
.amount-negative { color: #943526; }
.period-track { position: relative; height: .92rem; border-radius: 3px; background: #eef3f6; }
.period-zero { position: absolute; inset-block: -.15rem; border-inline-start: 1px solid #879daf; }
.period-bar { position: absolute; inset-block: .12rem; border-radius: 2px; background: var(--revenue-fill); }
.period-bar--negative { background: var(--refund-fill); }
.timeline-empty { margin: 0; padding: 1.4rem .8rem; border-radius: 7px; background: #f6f8fb; color: #475569; text-align: center; font-size: .86rem; }
@container (max-width: 33rem) {
  .timeline-axis, .timeline-row { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: .25rem .55rem; }
  .axis-labels, .period-track { grid-column: 1 / -1; }
  .period-track { grid-row: 2; }
  .timeline-row { padding-block: .55rem; }
}
@media print {
  .timeline-scroll { max-height: none; overflow: visible; }
  .timeline-axis { position: static; }
  .timeline-row { break-inside: avoid; }
  .revenue-timeline { print-color-adjust: exact; }
}
</style>
