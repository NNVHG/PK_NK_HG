import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { WorkspaceSnapshot } from '@/services/workspaces';
export const useWorkspacesStore = defineStore('MOD_MST_workspaces', () => {
  const snapshot = ref<WorkspaceSnapshot | null>(null);
  function replace(value: WorkspaceSnapshot) { snapshot.value = value; }
  function reset() { snapshot.value = null; }
  return { snapshot, replace, reset };
});
