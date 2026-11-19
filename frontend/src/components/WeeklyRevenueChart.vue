<template>
  <div class="weekly-revenue">
    <div v-if="loading && !days.length" class="chart-state" role="status">Đang tải doanh thu…</div>
    <div v-else-if="error && !days.length" class="chart-state chart-state--warning" role="alert">
      <span>Không tải được doanh thu. Vui lòng thử lại.</span>
      <button class="retry-button" type="button" :disabled="loading" @click="emit('retry')">Tải lại</button>
    </div>
    <template v-else>
      <div v-if="error" class="chart-notice" role="status">
        Chưa cập nhật được doanh thu. Đang hiển thị số liệu lần tải trước.
        <button class="retry-button" type="button" :disabled="loading" @click="emit('retry')">Tải lại</button>
      </div>
      <p v-if="loading" class="chart-notice" role="status">Đang cập nhật doanh thu…</p>
      <p v-if="missingDays" class="chart-notice" role="status">Thiếu số liệu {{ missingDays }} ngày. Dấu gạch biểu thị ngày chưa có dữ liệu.</p>
      <div v-if="empty" class="chart-state" role="status">Chưa ghi nhận giao dịch hoặc khoản hoàn trong 7 ngày này.</div>
      <template v-else-if="selected">
        <div class="selected-day" aria-live="polite" aria-atomic="true">
          <span class="selected-date">{{ selected.labels.long }}</span>
          <strong class="selected-revenue" :class="{ 'selected-revenue--negative': selected.revenue !== null && selected.revenue < 0 }">
            {{ selected.revenue === null ? 'Chưa có số liệu' : formatRevenue(selected.revenue) }}
          </strong>
          <span v-if="selected.transactionCount !== null" class="selected-transactions">{{ selected.transactionCount }} giao dịch thu</span>
        </div>

        <div class="weekly-plot">
          <div class="weekly-axis" aria-hidden="true">
            <span v-for="tick in scale.ticks" :key="tick" :style="{ bottom: scale.position(tick) + '%' }">
              {{ formatRevenueTick(tick) }}
            </span>
          </div>
          <div class="plot-body">
            <div class="plot-grid" aria-hidden="true">
              <span
                v-for="tick in scale.ticks" :key="tick"
                :class="{ 'plot-grid-zero': tick === 0 }" :style="{ bottom: scale.position(tick) + '%' }"
              ></span>
            </div>
            <div class="plot-days" role="group" aria-label="Doanh thu từng ngày, đơn vị đồng">
              <button
                v-for="day in plottedDays" :key="day.date" type="button" class="plot-day"
                :class="{ 'plot-day--active': day.date === selected.date }"
                :aria-label="day.accessibleLabel" :aria-pressed="day.date === selected.date"
                @click="selectedDate = day.date"
              >
                <span class="day-column" aria-hidden="true">
                  <span
                    v-if="day.revenue !== null && day.revenue !== 0"
                    class="day-bar" :class="{ 'day-bar--negative': day.revenue < 0 }"
                    :style="{ bottom: day.bar.start + '%', height: day.bar.size + '%' }"
                  ></span>
                  <span
                    v-else class="day-zero" :class="{ 'day-zero--missing': day.revenue === null }"
                    :style="{ bottom: scale.zero + '%' }"
                  ></span>
                </span>
                <span class="day-caption" aria-hidden="true">
                  <span>{{ day.labels.weekday }}</span>
                  <time :datetime="day.date">{{ day.labels.short }}</time>
                </span>
              </button>
            </div>
          </div>
        </div>
      </template>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import {
  createRevenueScale, formatRevenue, formatRevenueTick, revenueDateLabels, type RevenueChartDay,
} from '@/utils/revenueChart';

const props = defineProps<{ days: RevenueChartDay[]; loading: boolean; error: boolean }>();
const emit = defineEmits<{ retry: [] }>();
const selectedDate = ref<string>();
const scale = computed(() => createRevenueScale(props.days.map(day => day.revenue)));
const missingDays = computed(() => props.days.filter(day => day.revenue === null).length);
const empty = computed(() => props.days.length > 0 && props.days.every(day => day.revenue === 0 && day.transactionCount === 0));
const plottedDays = computed(() => props.days.map(day => {
  const labels = revenueDateLabels(day.date);
  const amount = day.revenue === null ? 'Chưa có số liệu' : formatRevenue(day.revenue);
  const transactions = day.transactionCount === null ? '' : `, ${day.transactionCount} giao dịch thu`;
  return { ...day, labels, bar: scale.value.bar(day.revenue ?? 0), accessibleLabel: `${labels.long}, ${amount}${transactions}` };
}));
const selected = computed(() => plottedDays.value.find(day => day.date === selectedDate.value) ?? plottedDays.value.at(-1));
</script>

<style scoped>
.weekly-revenue {
  --plot-height: clamp(7.5rem, 24vw, 10rem);
  --revenue-fill: #147d78;
  --refund-fill: #b94d38;
  display: grid;
  gap: .75rem;
  min-width: 0;
}
.selected-day {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: .3rem .85rem;
  min-width: 0;
  padding: .65rem .85rem;
  border-inline-start: 3px solid var(--revenue-fill);
  border-radius: 0 7px 7px 0;
  background: #f2f8f7;
  color: #475569;
  font-size: .81rem;
}
.selected-date { font-weight: 650; }
.selected-date::first-letter { text-transform: uppercase; }
.selected-revenue { font-size: 1.1rem; line-height: 1.2; color: #0b554f; overflow-wrap: anywhere; }
.selected-revenue--negative { color: #9d3929; }
.selected-transactions { margin-inline-start: auto; }
.weekly-plot {
  display: grid;
  grid-template-columns: 4.4ch minmax(0, 1fr);
  gap: .45rem;
  padding-block: .5rem .125rem;
}
.weekly-axis, .plot-grid, .day-column { height: var(--plot-height); }
.weekly-axis { position: relative; }
.weekly-axis > span {
  position: absolute;
  inset-inline-end: 0;
  transform: translateY(50%);
  font-size: .67rem;
  white-space: nowrap;
  color: #475569;
  font-variant-numeric: tabular-nums;
}
.plot-body, .day-column { position: relative; min-width: 0; }
.plot-grid { position: absolute; inset: 0 0 auto; pointer-events: none; }
.plot-grid > span { position: absolute; inset-inline: 0; border-block-start: 1px solid #e2e8f0; }
.plot-grid > .plot-grid-zero { border-color: #879daf; }
.plot-days { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); gap: .13rem; }
.plot-day {
  display: grid;
  grid-template-rows: var(--plot-height) auto;
  gap: .5rem;
  border: 0;
  border-radius: 7px;
  margin: 0;
  min-width: 0;
  padding: 0 0 .3rem;
  color: #364c61;
  background: transparent;
  cursor: pointer;
  font: inherit;
}
.plot-day:hover, .plot-day--active { background: rgb(20 125 120 / 8%); }
.plot-day:focus-visible, .retry-button:focus-visible { outline: 2px solid #194a78; outline-offset: 2px; }
.day-bar, .day-zero { position: absolute; inset-inline-start: 50%; transform: translateX(-50%); }
.day-bar { width: min(49%, 2.75rem); border-radius: 4px 4px 0 0; background: var(--revenue-fill); }
.day-bar--negative { border-radius: 0 0 4px 4px; background: var(--refund-fill); }
.plot-day--active .day-bar { outline: 1px solid #17334a; outline-offset: -1px; }
.day-zero { width: min(34%, 1rem); border-top: 2px solid #4b6478; }
.day-zero--missing { border-top: 2px dotted #526a7e; }
.day-caption {
  display: grid;
  gap: .15rem;
  justify-items: center;
  font-size: .72rem;
  font-weight: 700;
  line-height: 1.15;
}
.day-caption time { font-size: .67rem; font-weight: 500; font-variant-numeric: tabular-nums; }
.plot-day--active .day-caption { color: #0b554f; }
.chart-state, .chart-notice {
  margin: 0;
  padding: .65rem .8rem;
  border-radius: 7px;
  color: #475569;
  background: #f6f8fb;
  font-size: .84rem;
}
.chart-state { display: grid; min-height: 9rem; place-content: center; justify-items: center; gap: .7rem; text-align: center; }
.chart-state--warning { background: #fff7ed; color: #854319; }
.retry-button { padding: .35rem .65rem; border: 1px solid #c6d5e1; border-radius: 6px; color: #183a58; background: #fff; font: inherit; font-weight: 700; cursor: pointer; }
.retry-button:disabled { opacity: .55; cursor: wait; }
@media (max-width: 480px) {
  .day-caption time { font-size: .61rem; }
  .selected-transactions { margin-inline-start: 0; }
}
</style>
