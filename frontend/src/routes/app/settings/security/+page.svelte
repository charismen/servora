<script lang="ts">
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { createTranslator } from '$lib/i18n';
	import { toast } from '$lib/stores/toast.svelte';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const status = $derived(data.status);

	let busy = $state(false);
	let copiedCodes = $state(false);

	function copyRecoveryCodes(codes: string[]) {
		if (typeof navigator === 'undefined' || !navigator.clipboard) return;
		navigator.clipboard.writeText(codes.join('\n')).then(() => {
			copiedCodes = true;
			toast.success(t('security.recoveryCopied'));
			setTimeout(() => (copiedCodes = false), 2500);
		});
	}

	const totpQrUrl = $derived(
		form?.totpSetup?.otpAuthUri
			? `https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(form.totpSetup.otpAuthUri)}`
			: ''
	);
</script>

<svelte:head>
	<title>{t('security.title')} · {t('brand.name')}</title>
</svelte:head>

<div class="space-y-lg pb-16 max-w-4xl">
	<PageHeader
		items={[
			{ label: t('nav.settings'), href: '/app/settings' },
			{ label: t('security.title') }
		]}
		title={t('security.title')}
		subtitle={t('security.subtitle')}
	/>

	{#if status.enforcementRequired && !status.enabled}
		<div class="rounded-xl border border-amber-300 bg-amber-50 p-4 flex items-start gap-3 text-sm text-amber-950">
			<span class="material-symbols-outlined text-[20px] text-amber-700 mt-0.5 shrink-0">shield_lock</span>
			<div>
				<p class="font-semibold">{t('security.policyRequiredTitle')}</p>
				<p class="text-xs text-amber-900 mt-0.5">{t('security.policyRequiredBody')}</p>
			</div>
		</div>
	{/if}

	<!-- Recovery Codes Banner (shown immediately after enabling or regenerating) -->
	{#if form?.recoveryCodes && form.recoveryCodes.length > 0}
		<section class="rounded-2xl border-2 border-[#b45039]/40 bg-[#faf8f5] p-5 space-y-4">
			<div class="flex flex-wrap items-start justify-between gap-3">
				<div>
					<h2 class="text-base font-bold text-[#1c1815] flex items-center gap-2">
						<span class="material-symbols-outlined text-[20px] text-[#b45039]">key</span>
						<span>{t('security.recoveryCodesTitle')}</span>
					</h2>
					<p class="text-xs text-[#6b6158] mt-1">{t('security.recoveryCodesWarning')}</p>
				</div>
				<button
					type="button"
					class="inline-flex items-center gap-1.5 rounded-lg border border-[#e6dfd5] bg-white px-3 py-1.5 text-xs font-semibold text-[#1c1815] hover:bg-[#f4f0ea] cursor-pointer"
					onclick={() => copyRecoveryCodes(form?.recoveryCodes ?? [])}
				>
					<span class="material-symbols-outlined text-[15px]">{copiedCodes ? 'check' : 'content_copy'}</span>
					<span>{copiedCodes ? t('common.copied') : t('security.copyCodes')}</span>
				</button>
			</div>
			<div class="grid grid-cols-2 sm:grid-cols-4 gap-2">
				{#each form.recoveryCodes as code}
					<div class="rounded-lg border border-[#e6dfd5] bg-white px-3 py-2 text-center font-mono text-sm font-semibold text-[#1c1815]">
						{code}
					</div>
				{/each}
			</div>
		</section>
	{/if}

	<!-- Current Status Summary -->
	<section class="rounded-2xl border border-[#e6dfd5] bg-white p-6 shadow-2xs flex flex-wrap items-center justify-between gap-4">
		<div class="space-y-1">
			<div class="flex items-center gap-2.5">
				<h2 class="text-base font-semibold text-[#1c1815]">{t('security.twoFactorHeading')}</h2>
				<span
					class="inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold {status.enabled
						? 'bg-emerald-100 text-emerald-800'
						: 'bg-[#f4f0ea] text-[#6b6158]'}"
				>
					<span class="w-1.5 h-1.5 rounded-full {status.enabled ? 'bg-emerald-600' : 'bg-[#6b6158]'}"></span>
					{status.enabled ? t('security.statusEnabled') : t('security.statusDisabled')}
				</span>
			</div>
			<p class="text-xs text-[#6b6158]">
				{#if status.enabled}
					{t('security.activeMethodLabel')}:
					<strong class="text-[#1c1815]">
						{status.method === 'EmailOtp' ? t('security.methodEmailOtp') : t('security.methodTotp')}
					</strong>
					· {t('security.recoveryCodesRemaining')}: <strong class="text-[#1c1815]">{status.recoveryCodesLeft}</strong>
				{:else}
					{t('security.twoFactorDesc')}
				{/if}
			</p>
		</div>
	</section>

	<!-- Setup / Switch Methods -->
	<div class="grid grid-cols-1 md:grid-cols-2 gap-5">
		<!-- Authenticator App (TOTP) -->
		<section class="rounded-2xl border border-[#e6dfd5] bg-white p-6 shadow-2xs space-y-4">
			<div class="flex items-start gap-3">
				<div class="w-10 h-10 rounded-xl bg-[#f4f0ea] text-[#1c1815] flex items-center justify-center shrink-0">
					<span class="material-symbols-outlined text-[20px]">qr_code_2</span>
				</div>
				<div>
					<h3 class="text-sm font-bold text-[#1c1815]">{t('security.methodTotp')}</h3>
					<p class="text-xs text-[#6b6158] mt-0.5">{t('security.methodTotpDesc')}</p>
				</div>
			</div>

			{#if form?.totpSetup}
				<div class="space-y-4 pt-2 border-t border-[#e6dfd5]">
					<div class="flex flex-col items-center gap-3 rounded-xl bg-[#faf8f5] p-4 border border-[#e6dfd5]">
						<img src={totpQrUrl} alt="Authenticator QR code" class="w-44 h-44 rounded-lg bg-white p-2 border border-[#e6dfd5]" />
						<div class="text-center">
							<p class="text-[11px] uppercase tracking-wider text-[#6b6158] font-semibold">{t('security.manualKeyLabel')}</p>
							<code class="text-xs font-mono font-semibold text-[#1c1815] select-all">{form.totpSetup.sharedKey}</code>
						</div>
					</div>

					<form
						method="POST"
						action="?/confirmTotp"
						class="space-y-3"
						use:enhance={() => {
							busy = true;
							return async ({ update }) => {
								await update();
								busy = false;
							};
						}}
					>
						<div class="space-y-1">
							<label for="totp-confirm-code" class="block text-xs font-semibold text-[#6b6158]">
								{t('security.enterSixDigitCode')}
							</label>
							<input
								id="totp-confirm-code"
								name="code"
								type="text"
								inputmode="numeric"
								placeholder="000000"
								required
								class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 font-mono text-center tracking-widest text-sm focus:outline-none focus:ring-1 focus:ring-[#1c1815]"
							/>
						</div>
						<button
							type="submit"
							disabled={busy}
							class="w-full rounded-lg bg-[#1c1815] px-4 py-2 text-xs font-semibold text-white hover:opacity-90 cursor-pointer disabled:opacity-50"
						>
							{t('security.confirmAndEnable')}
						</button>
					</form>
				</div>
			{:else}
				<form
					method="POST"
					action="?/setupTotp"
					use:enhance={() => {
						busy = true;
						return async ({ update }) => {
							await update();
							busy = false;
						};
					}}
				>
					<button
						type="submit"
						disabled={busy}
						class="w-full rounded-lg border border-[#1c1815] bg-[#1c1815] px-4 py-2 text-xs font-semibold text-white hover:opacity-90 cursor-pointer disabled:opacity-50"
					>
						{status.enabled && status.method === 'AuthenticatorTotp' ? t('security.reconfigureTotp') : t('security.setupTotp')}
					</button>
				</form>
			{/if}
		</section>

		<!-- Email 6-Digit OTP -->
		<section class="rounded-2xl border border-[#e6dfd5] bg-white p-6 shadow-2xs space-y-4">
			<div class="flex items-start gap-3">
				<div class="w-10 h-10 rounded-xl bg-[#f4f0ea] text-[#1c1815] flex items-center justify-center shrink-0">
					<span class="material-symbols-outlined text-[20px]">mark_email_read</span>
				</div>
				<div>
					<h3 class="text-sm font-bold text-[#1c1815]">{t('security.methodEmailOtp')}</h3>
					<p class="text-xs text-[#6b6158] mt-0.5">
						{t('security.methodEmailOtpDesc')}
						{#if status.maskedEmail}
							<span class="font-mono text-[#1c1815]"> ({status.maskedEmail})</span>
						{/if}
					</p>
				</div>
			</div>

			{#if !status.emailOtpAvailable}
				<div class="rounded-xl bg-[#f4f0ea] px-3.5 py-2.5 text-xs text-[#6b6158]">
					{t('security.emailOtpUnavailable')}
				</div>
			{:else if form?.emailEnrollment}
				<form
					method="POST"
					action="?/confirmEmailOtp"
					class="space-y-3 pt-2 border-t border-[#e6dfd5]"
					use:enhance={() => {
						busy = true;
						return async ({ update }) => {
							await update();
							busy = false;
						};
					}}
				>
					<input type="hidden" name="challengeToken" value={form.emailEnrollment.challengeToken} />
					<input type="hidden" name="maskedEmail" value={form.emailEnrollment.maskedEmail} />
					<p class="text-xs text-[#6b6158]">
						{t('security.emailOtpSentTo')} <strong class="font-mono text-[#1c1815]">{form.emailEnrollment.maskedEmail}</strong>
					</p>
					<div class="space-y-1">
						<label for="email-otp-code" class="block text-xs font-semibold text-[#6b6158]">
							{t('security.enterSixDigitCode')}
						</label>
						<input
							id="email-otp-code"
							name="code"
							type="text"
							inputmode="numeric"
							placeholder="000000"
							required
							class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 font-mono text-center tracking-widest text-sm focus:outline-none focus:ring-1 focus:ring-[#1c1815]"
						/>
					</div>
					<button
						type="submit"
						disabled={busy}
						class="w-full rounded-lg bg-[#1c1815] px-4 py-2 text-xs font-semibold text-white hover:opacity-90 cursor-pointer disabled:opacity-50"
					>
						{t('security.confirmAndEnable')}
					</button>
				</form>
			{:else}
				<form
					method="POST"
					action="?/startEmailOtp"
					use:enhance={() => {
						busy = true;
						return async ({ update }) => {
							await update();
							busy = false;
						};
					}}
				>
					<button
						type="submit"
						disabled={busy}
						class="w-full rounded-lg border border-[#e6dfd5] bg-[#faf8f5] px-4 py-2 text-xs font-semibold text-[#1c1815] hover:bg-[#f4f0ea] cursor-pointer disabled:opacity-50"
					>
						{t('security.setupEmailOtp')}
					</button>
				</form>
			{/if}
		</section>
	</div>

	<!-- Recovery Codes & Disable (only when 2FA enabled) -->
	{#if status.enabled}
		<div class="grid grid-cols-1 md:grid-cols-2 gap-5">
			<section class="rounded-2xl border border-[#e6dfd5] bg-white p-6 shadow-2xs space-y-3">
				<h3 class="text-sm font-bold text-[#1c1815]">{t('security.regenerateRecoveryTitle')}</h3>
				<p class="text-xs text-[#6b6158]">{t('security.regenerateRecoveryDesc')}</p>
				<form
					method="POST"
					action="?/regenerateRecovery"
					class="space-y-3"
					use:enhance={() => {
						busy = true;
						return async ({ update }) => {
							await update();
							busy = false;
						};
					}}
				>
					<input
						name="currentPassword"
						type="password"
						required
						placeholder={t('security.currentPasswordPlaceholder')}
						class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm focus:outline-none focus:ring-1 focus:ring-[#1c1815]"
					/>
					<button
						type="submit"
						disabled={busy}
						class="rounded-lg border border-[#e6dfd5] bg-[#faf8f5] px-4 py-2 text-xs font-semibold text-[#1c1815] hover:bg-[#f4f0ea] cursor-pointer disabled:opacity-50"
					>
						{t('security.regenerateRecoveryBtn')}
					</button>
				</form>
			</section>

			<section class="rounded-2xl border border-red-200 bg-white p-6 shadow-2xs space-y-3">
				<h3 class="text-sm font-bold text-red-800">{t('security.disableTitle')}</h3>
				<p class="text-xs text-[#6b6158]">
					{status.enforcementRequired ? t('security.disableBlockedByPolicy') : t('security.disableDesc')}
				</p>
				<form
					method="POST"
					action="?/disable2fa"
					class="space-y-3"
					use:enhance={() => {
						busy = true;
						return async ({ update }) => {
							await update();
							busy = false;
						};
					}}
				>
					<input
						name="currentPassword"
						type="password"
						required
						disabled={status.enforcementRequired}
						placeholder={t('security.currentPasswordPlaceholder')}
						class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm focus:outline-none focus:ring-1 focus:ring-red-700 disabled:bg-[#f4f0ea]"
					/>
					<button
						type="submit"
						disabled={busy || status.enforcementRequired}
						class="rounded-lg bg-red-700 px-4 py-2 text-xs font-semibold text-white hover:bg-red-800 cursor-pointer disabled:opacity-40"
					>
						{t('security.disableBtn')}
					</button>
				</form>
			</section>
		</div>
	{/if}
</div>
