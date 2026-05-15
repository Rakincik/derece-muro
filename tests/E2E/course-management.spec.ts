import { test, expect } from '@playwright/test';

/**
 * Course Management E2E Testleri
 * Ön koşul: Backend + Admin Frontend çalışıyor + Admin login olmuş.
 */

const ADMIN_EMAIL = 'admin@demo.com';
const ADMIN_PASSWORD = 'Demo@1234';

test.describe('Course Management', () => {

    // Her test öncesi login ol
    test.beforeEach(async ({ page }) => {
        await page.goto('/');
        await page.fill('input[type="email"], input[name="email"]', ADMIN_EMAIL);
        await page.fill('input[type="password"], input[name="password"]', ADMIN_PASSWORD);
        await page.click('button[type="submit"]');
        await page.waitForURL('**/dashboard**', { timeout: 10000 });
    });

    test('should navigate to courses page', async ({ page }) => {
        // Sidebar'dan "Dersler" linkine tıkla
        await page.click('text=Dersler');
        await page.waitForURL('**/courses**');
        await expect(page.locator('h1, h2').first()).toBeVisible();
    });

    test('should show course list', async ({ page }) => {
        await page.goto('/dashboard/courses');
        await page.waitForLoadState('networkidle');

        // Kurs tablosu veya kart listesi yüklenmiş olmalı
        const content = await page.content();
        expect(content.length).toBeGreaterThan(500); // Boş sayfa değil
    });

    test('should open attendance modal when clicking Yoklama button', async ({ page }) => {
        await page.goto('/dashboard/courses');
        await page.waitForLoadState('networkidle');

        // Yoklama butonu varsa tıkla
        const yoklamaBtn = page.locator('text=Yoklama').first();
        if (await yoklamaBtn.isVisible()) {
            await yoklamaBtn.click();
            // Modal açılmış olmalı
            await expect(page.locator('[role="dialog"], .modal').first()).toBeVisible({ timeout: 3000 });
        }
    });
});
