import { test, expect } from '@playwright/test';

/**
 * Admin Login E2E Testleri
 * Ön koşul: Backend (localhost:5000) + Admin Frontend (localhost:3001) çalışıyor olmalı.
 */

const ADMIN_EMAIL = 'admin@demo.com';
const ADMIN_PASSWORD = 'Demo@1234';

test.describe('Admin Login', () => {

    test('should login successfully and see dashboard', async ({ page }) => {
        await page.goto('/');

        // Login form
        await page.fill('input[type="email"], input[name="email"]', ADMIN_EMAIL);
        await page.fill('input[type="password"], input[name="password"]', ADMIN_PASSWORD);
        await page.click('button[type="submit"]');

        // Dashboard'a yönlendirilmeli
        await page.waitForURL('**/dashboard**', { timeout: 10000 });
        await expect(page.locator('text=Dashboard')).toBeVisible();
    });

    test('should show error on wrong password', async ({ page }) => {
        await page.goto('/');

        await page.fill('input[type="email"], input[name="email"]', ADMIN_EMAIL);
        await page.fill('input[type="password"], input[name="password"]', 'WrongPass!');
        await page.click('button[type="submit"]');

        // Hata mesajı gösterilmeli
        await expect(page.locator('[role="alert"], .toast-error, .error-message, text=Hata').first()).toBeVisible({ timeout: 5000 });
    });

    test('should redirect to login when not authenticated', async ({ page }) => {
        // Dashboard'a doğrudan gitmeye çalış
        await page.goto('/dashboard');

        // Login sayfasına yönlendirilmeli
        await page.waitForURL('**/', { timeout: 5000 });
        await expect(page.locator('input[type="email"], input[name="email"]')).toBeVisible();
    });
});
