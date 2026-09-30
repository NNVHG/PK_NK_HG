<template>
  <section class="profile-page">
    <header class="page-heading">
      <div>
        <RouterLink class="back-link" to="/">← Bảng điều khiển</RouterLink>
        <p class="eyebrow">F_AUTH_03</p>
        <h1>Hồ sơ cá nhân</h1>
        <p class="subtitle">Quản lý thông tin cá nhân và bảo mật tài khoản.</p>
      </div>
    </header>

    <div v-if="loadingProfile" class="state-message">Đang tải hồ sơ...</div>
    <div v-else class="profile-grid">
      <section class="panel">
        <div class="panel-heading">
          <div class="icon-badge" aria-hidden="true">●</div>
          <div>
            <h2>Thông tin cá nhân</h2>
            <p>Số điện thoại dùng để đăng nhập và không thể thay đổi tại đây.</p>
          </div>
        </div>

        <form class="form" @submit.prevent="handleUpdateProfile">
          <div class="field">
            <label for="fullName">Họ và tên <span aria-hidden="true">*</span></label>
            <input id="fullName" v-model="fullName" autocomplete="name" maxlength="100" required />
          </div>

          <div class="field">
            <label for="phone">Số điện thoại</label>
            <input id="phone" :value="phone" type="tel" readonly aria-readonly="true" />
            <small>Số điện thoại hiện dùng để đăng nhập.</small>
          </div>

          <div class="field">
            <label for="email">Email</label>
            <input id="email" v-model="email" type="email" autocomplete="email" maxlength="100" />
          </div>

          <div class="form-row">
            <div class="field">
              <label for="dateOfBirth">Ngày sinh</label>
              <input id="dateOfBirth" v-model="dateOfBirth" type="date" />
            </div>
            <div class="field">
              <label for="gender">Giới tính</label>
              <select id="gender" v-model="gender">
                <option value="">Chưa cung cấp</option>
                <option value="Male">Nam</option>
                <option value="Female">Nữ</option>
                <option value="Other">Khác</option>
              </select>
            </div>
          </div>

          <p v-if="profileError" class="alert alert-error" role="alert">{{ profileError }}</p>
          <p v-if="profileMessage" class="alert alert-success" role="status">{{ profileMessage }}</p>
          <button class="button button-primary" type="submit" :disabled="profileSaving">
            {{ profileSaving ? 'Đang lưu...' : 'Lưu thay đổi' }}
          </button>
        </form>
      </section>

      <section class="panel password-panel">
        <div class="panel-heading">
          <div class="icon-badge lock" aria-hidden="true">●</div>
          <div>
            <h2>Đổi mật khẩu</h2>
            <p>Nhập mật khẩu hiện tại để đặt mật khẩu mới.</p>
          </div>
        </div>

        <form class="form" @submit.prevent="handleChangePassword">
          <div class="field">
            <label for="oldPassword">Mật khẩu cũ</label>
            <input id="oldPassword" v-model="oldPassword" type="password" autocomplete="current-password" required />
          </div>
          <div class="field">
            <label for="newPassword">Mật khẩu mới</label>
            <input
              id="newPassword"
              v-model="newPassword"
              type="password"
              autocomplete="new-password"
              placeholder="Tối thiểu 6 ký tự: 1 hoa, 1 thường, 1 số"
              required
            />
          </div>
          <div class="field">
            <label for="confirmPassword">Xác nhận mật khẩu mới</label>
            <input id="confirmPassword" v-model="confirmPassword" type="password" autocomplete="new-password" required />
          </div>

          <p v-if="passwordError" class="alert alert-error" role="alert">{{ passwordError }}</p>
          <p v-if="passwordMessage" class="alert alert-success" role="status">{{ passwordMessage }}</p>
          <button class="button button-secondary" type="submit" :disabled="passwordSaving">
            {{ passwordSaving ? 'Đang xử lý...' : 'Cập nhật mật khẩu' }}
          </button>
        </form>
      </section>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { apiClient } from '@/services/api';

const authStore = useAuthStore();
const loadingProfile = ref(true);
const profileSaving = ref(false);
const profileError = ref('');
const profileMessage = ref('');
const fullName = ref('');
const email = ref('');
const dateOfBirth = ref('');
const gender = ref('');
const oldPassword = ref('');
const newPassword = ref('');
const confirmPassword = ref('');
const passwordSaving = ref(false);
const passwordError = ref('');
const passwordMessage = ref('');

const phone = computed(() => authStore.user?.phone ?? '');

onMounted(async () => {
  await authStore.fetchMe();
  fullName.value = authStore.user?.fullName ?? '';
  email.value = authStore.user?.email ?? '';
  dateOfBirth.value = authStore.user?.dateOfBirth ?? '';
  gender.value = authStore.user?.gender ?? '';
  loadingProfile.value = false;
});

function validateProfile(): string | null {
  if (!fullName.value.trim()) return 'Họ tên không được để trống.';
  if (email.value.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim())) {
    return 'Email không đúng định dạng.';
  }
  return null;
}

function backendError(error: any, fallback: string): string {
  const data = error.response?.data;
  if (Array.isArray(data?.errors)) {
    const messages = data.errors.map((item: any) => item?.message).filter(Boolean);
    if (messages.length) return messages.join(' ');
  }
  return data?.message || fallback;
}

async function handleUpdateProfile() {
  profileError.value = '';
  profileMessage.value = '';
  const validationError = validateProfile();
  if (validationError) {
    profileError.value = validationError;
    return;
  }

  profileSaving.value = true;
  try {
    const updated = await authStore.updateProfile({
      fullName: fullName.value.trim(),
      phone: phone.value,
      email: email.value.trim() || null,
      dateOfBirth: dateOfBirth.value || null,
      gender: gender.value || null
    });
    fullName.value = updated.fullName;
    email.value = updated.email ?? '';
    dateOfBirth.value = updated.dateOfBirth ?? '';
    gender.value = updated.gender ?? '';
    profileMessage.value = 'Cập nhật hồ sơ thành công.';
  } catch (error: any) {
    profileError.value = backendError(error, 'Cập nhật hồ sơ thất bại. Vui lòng thử lại.');
  } finally {
    profileSaving.value = false;
  }
}

async function handleChangePassword() {
  passwordError.value = '';
  passwordMessage.value = '';
  if (newPassword.value !== confirmPassword.value) {
    passwordError.value = 'Mật khẩu mới và xác nhận mật khẩu không trùng khớp.';
    return;
  }

  passwordSaving.value = true;
  try {
    const response = await apiClient.post('/auth/change-password', {
      oldPassword: oldPassword.value,
      newPassword: newPassword.value
    });
    passwordMessage.value = response.data?.message || 'Đổi mật khẩu thành công!';
    oldPassword.value = '';
    newPassword.value = '';
    confirmPassword.value = '';
  } catch (error: any) {
    passwordError.value = backendError(error, 'Đổi mật khẩu thất bại. Vui lòng thử lại.');
  } finally {
    passwordSaving.value = false;
  }
}
</script>

<style scoped>
.profile-page {
  max-width: 1040px;
  margin: 0 auto;
  color: #223247;
}

.page-heading {
  margin-bottom: 1.5rem;
}

.back-link {
  display: inline-block;
  margin-bottom: 1rem;
  color: #3979a8;
  font-size: 0.9rem;
  font-weight: 600;
  text-decoration: none;
}

.eyebrow {
  margin: 0 0 0.25rem;
  color: #3980ad;
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.12em;
}

h1 {
  margin: 0;
  font-size: clamp(1.7rem, 3vw, 2.2rem);
}

.subtitle,
.panel-heading p {
  color: #6b7b8d;
  line-height: 1.5;
}

.subtitle {
  margin: 0.45rem 0 0;
}

.profile-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) minmax(300px, 0.8fr);
  align-items: start;
  gap: 1.25rem;
}

.panel {
  padding: 1.5rem;
  border: 1px solid #e3eaf0;
  border-radius: 14px;
  background: #fff;
  box-shadow: 0 8px 24px rgba(30, 56, 79, 0.06);
}

.panel-heading {
  display: flex;
  align-items: flex-start;
  gap: 0.85rem;
  margin-bottom: 1.35rem;
}

.icon-badge {
  display: grid;
  width: 2.35rem;
  height: 2.35rem;
  flex: 0 0 auto;
  place-items: center;
  border-radius: 10px;
  background: #e8f4fb;
  color: #3980ad;
  font-size: 0.65rem;
}

.icon-badge.lock {
  background: #edf2f7;
  color: #52677a;
}

.panel-heading h2 {
  margin: 0 0 0.25rem;
  font-size: 1.12rem;
}

.panel-heading p {
  margin: 0;
  font-size: 0.85rem;
}

.form {
  display: grid;
  gap: 1rem;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.field label {
  display: block;
  margin-bottom: 0.4rem;
  color: #36495b;
  font-size: 0.86rem;
  font-weight: 600;
}

.field label span {
  color: #d44747;
}

.field input,
.field select {
  box-sizing: border-box;
  width: 100%;
  min-height: 2.7rem;
  padding: 0.65rem 0.75rem;
  border: 1px solid #d3dde6;
  border-radius: 8px;
  background: #fff;
  color: #26394c;
  font: inherit;
  font-size: 0.92rem;
}

.field input:focus,
.field select:focus {
  border-color: #4b93bf;
  outline: 3px solid rgba(75, 147, 191, 0.14);
}

.field input[readonly] {
  background: #f3f6f8;
  color: #657587;
}

.field small {
  display: block;
  margin-top: 0.35rem;
  color: #7a8997;
  font-size: 0.76rem;
}

.alert {
  margin: 0;
  padding: 0.7rem 0.85rem;
  border-radius: 8px;
  font-size: 0.87rem;
  line-height: 1.45;
}

.alert-error {
  background: #fff0ef;
  color: #a52b25;
}

.alert-success {
  background: #e8f7ef;
  color: #176341;
}

.button {
  min-height: 2.8rem;
  padding: 0.7rem 1rem;
  border: 0;
  border-radius: 8px;
  color: #fff;
  font: inherit;
  font-weight: 700;
  cursor: pointer;
  transition: filter 0.15s ease, opacity 0.15s ease;
}

.button:hover:not(:disabled) {
  filter: brightness(0.95);
}

.button:disabled {
  cursor: wait;
  opacity: 0.65;
}

.button-primary {
  background: #3181ad;
}

.button-secondary {
  background: #344d63;
}

.state-message {
  padding: 2rem;
  border-radius: 12px;
  background: #fff;
  color: #657587;
  text-align: center;
}

@media (max-width: 760px) {
  .profile-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 480px) {
  .panel {
    padding: 1.15rem;
  }

  .form-row {
    grid-template-columns: 1fr;
  }
}
</style>
