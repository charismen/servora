<script lang="ts">
	import { untrack } from 'svelte';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { createTranslator, resolveTerminology } from '$lib/i18n';
	import { handlePhoneInput, handlePhonePaste } from '$lib/utils/phone';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const terms = $derived(resolveTerminology(data.user?.workspace?.terminology, data.locale));
	let values = $derived(form?.values);
	let requesterType = $state<string>(
		untrack(() =>
			values?.requesterType ??
			(data.user?.workspace?.operatingMode === 'InternalMaintenance' ? 'Department' : 'Customer')
		)
	);

	let isSaving = $state(false);
	let formElement = $state<HTMLFormElement | null>(null);

	function handleKeydown(e: KeyboardEvent) {
		if ((e.metaKey || e.ctrlKey) && e.key === 'Enter') {
			e.preventDefault();
			if (!isSaving && formElement) {
				formElement.requestSubmit();
			}
		}
	}
</script>

<svelte:window onkeydown={handleKeydown} />

<div class="space-y-lg">
	<PageHeader
		items={[
			{ label: t('common.console'), href: '/app' },
			{ label: terms.requesterPlural, href: '/app/customers' },
			{ label: `+ ${terms.requesterSingular}` }
		]}
		title={`${t('customers.form.newCustomer')} (${terms.requesterSingular})`}
		subtitle={t('customers.form.registerSubtitle')}
	/>

	<!-- Form Layout: Main Form + Sidebar Guidance -->
	<form
		bind:this={formElement}
		method="POST"
		onsubmit={() => (isSaving = true)}
		class="grid grid-cols-1 lg:grid-cols-12 gap-lg items-start"
	>
		<!-- Left: Main Form Fields -->
		<div class="lg:col-span-8 space-y-6">
			<div class="rounded-2xl border border-stone bg-white p-6 sm:p-7 shadow-2xs space-y-6">
				<!-- Section 1: Contact Details -->
				<div class="space-y-4">
					<div class="flex items-center gap-2 border-b border-stone/60 pb-3">
						<span class="material-symbols-outlined text-secondary text-[20px]">person</span>
						<h3 class="font-subheading text-[15px] font-bold text-ink">
							{t('customers.form.personalInfo')}
						</h3>
					</div>

					<div class="grid grid-cols-1 sm:grid-cols-2 gap-md">
						<div class="space-y-1 sm:col-span-2">
							<label for="requesterType" class="block font-label-caps text-[11px] font-bold uppercase tracking-wider text-secondary">
								{t('customers.form.requesterType')}
							</label>
							<select
								id="requesterType"
								name="requesterType"
								bind:value={requesterType}
								class="w-full rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-ink focus:border-ink focus:outline-none"
							>
								<option value="Customer">{t('customers.type.Customer')}</option>
								<option value="Department">{t('customers.type.Department')}</option>
								<option value="InternalEmployee">{t('customers.type.InternalEmployee')}</option>
								<option value="Branch">{t('customers.type.Branch')}</option>
							</select>
						</div>

						<!-- Full Name -->
						<div class="space-y-1 sm:col-span-2">
							<label for="fullName" class="block font-label-caps text-[11px] font-bold uppercase tracking-wider text-secondary">
								{t('customers.form.fullName')} <span class="text-error">*</span>
							</label>
							<div class="relative">
								<input
									id="fullName"
									name="fullName"
									value={values?.fullName ?? ''}
									required
									type="text"
									class="w-full rounded-input border border-stone bg-white pl-9 pr-3 py-2 text-body-sm text-ink placeholder:text-secondary/60 focus:border-ink focus:outline-none"
									placeholder={t('customers.form.fullNamePlaceholder')}
								/>
								<span class="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-[17px] text-secondary pointer-events-none">
									badge
								</span>
							</div>
						</div>

						<!-- Phone Number -->
						<div class="space-y-1 sm:col-span-2">
							<div class="flex items-center justify-between">
								<label for="phoneNumber" class="block font-label-caps text-[11px] font-bold uppercase tracking-wider text-secondary">
									{t('customers.form.phoneNumber')}
									{#if requesterType === 'Customer'}<span class="text-error">*</span>{/if}
								</label>
								<span class="text-[11px] text-emerald-700 font-medium inline-flex items-center gap-1">
									<span class="material-symbols-outlined text-[13px]">chat</span>
									{requesterType === 'Customer' ? t('customers.form.phoneHelp') : t('customers.form.phoneOptionalInternal')}
								</span>
							</div>
							<div class="relative">
								<input
									id="phoneNumber"
									name="phoneNumber"
									value={values?.phoneNumber ?? ''}
									required={requesterType === 'Customer'}
									type="tel"
									oninput={handlePhoneInput}
									onpaste={handlePhonePaste}
									class="w-full rounded-input border border-stone bg-white pl-9 pr-3 py-2 text-body-sm text-ink font-mono placeholder:font-sans placeholder:text-secondary/60 focus:border-ink focus:outline-none"
									placeholder={t('customers.form.phonePlaceholder')}
								/>
								<span class="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-[17px] text-secondary pointer-events-none">
									phone
								</span>
							</div>
						</div>

						{#if requesterType === 'Customer'}
							<div class="space-y-1 sm:col-span-2">
								<label for="companyName" class="block font-label-caps text-[11px] font-bold uppercase tracking-wider text-secondary">
									{t('customers.form.companyName')}
								</label>
								<input
									id="companyName"
									name="companyName"
									value={values?.companyName ?? ''}
									type="text"
									class="w-full rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-ink focus:border-ink focus:outline-none"
									placeholder={t('customers.form.companyNamePlaceholder')}
								/>
							</div>
						{:else}
							<div class="space-y-1">
								<label for="departmentName" class="block font-label-caps text-[11px] font-bold uppercase tracking-wider text-secondary">
									{t('customers.form.departmentName')}
								</label>
								<input
									id="departmentName"
									name="departmentName"
									value={values?.departmentName ?? ''}
									type="text"
									class="w-full rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-ink focus:border-ink focus:outline-none"
									placeholder={t('customers.form.departmentNamePlaceholder')}
								/>
							</div>
							<div class="space-y-1">
								<label for="departmentCode" class="block font-label-caps text-[11px] font-bold uppercase tracking-wider text-secondary">
									{t('customers.form.departmentCode')}
								</label>
								<input
									id="departmentCode"
									name="departmentCode"
									value={values?.departmentCode ?? ''}
									type="text"
									class="w-full rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-ink focus:border-ink focus:outline-none"
									placeholder={t('customers.form.departmentCodePlaceholder')}
								/>
							</div>
							<div class="space-y-1">
								<label for="costCenter" class="block font-label-caps text-[11px] font-bold uppercase tracking-wider text-secondary">
									{t('customers.form.costCenter')}
								</label>
								<input
									id="costCenter"
									name="costCenter"
									value={values?.costCenter ?? ''}
									type="text"
									class="w-full rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-ink focus:border-ink focus:outline-none"
									placeholder={t('customers.form.costCenterPlaceholder')}
								/>
							</div>
							<div class="space-y-1">
								<label for="employeeId" class="block font-label-caps text-[11px] font-bold uppercase tracking-wider text-secondary">
									{t('customers.form.employeeId')}
								</label>
								<input
									id="employeeId"
									name="employeeId"
									value={values?.employeeId ?? ''}
									type="text"
									class="w-full rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-ink focus:border-ink focus:outline-none"
									placeholder={t('customers.form.employeeIdPlaceholder')}
								/>
							</div>
						{/if}

						<!-- Email Address -->
						<div class="space-y-1 sm:col-span-2">
							<label for="email" class="block font-label-caps text-[11px] font-bold uppercase tracking-wider text-secondary">
								{t('customers.form.email')}
							</label>
							<div class="relative">
								<input
									id="email"
									name="email"
									value={values?.email ?? ''}
									type="email"
									class="w-full rounded-input border border-stone bg-white pl-9 pr-3 py-2 text-body-sm text-ink placeholder:text-secondary/60 focus:border-ink focus:outline-none"
									placeholder={t('customers.form.emailPlaceholder')}
								/>
								<span class="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-[17px] text-secondary pointer-events-none">
									mail
								</span>
							</div>
						</div>
					</div>
				</div>

				<!-- Section 2: Address & Location -->
				<div class="space-y-4 pt-2 border-t border-stone/80">
					<div class="flex items-center gap-2 border-b border-stone/60 pb-3">
						<span class="material-symbols-outlined text-secondary text-[20px]">home_pin</span>
						<h3 class="font-subheading text-[15px] font-bold text-ink">
							{t('customers.form.addressSection')}
						</h3>
					</div>

					<div class="space-y-1">
						<label for="address" class="block font-label-caps text-[11px] font-bold uppercase tracking-wider text-secondary">
							{t('customers.form.address')}
						</label>
						<input
							id="address"
							name="address"
							value={values?.address ?? ''}
							type="text"
							class="w-full rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-ink placeholder:text-secondary/60 focus:border-ink focus:outline-none"
							placeholder={t('customers.form.addressPlaceholder')}
						/>
					</div>
				</div>

				<!-- Section 3: Internal Notes -->
				<div class="space-y-4 pt-2 border-t border-stone/80">
					<div class="flex items-center gap-2 border-b border-stone/60 pb-3">
						<span class="material-symbols-outlined text-secondary text-[20px]">sticky_note_2</span>
						<h3 class="font-subheading text-[15px] font-bold text-ink">
							{t('customers.form.internalNotes')}
						</h3>
					</div>

					<div class="space-y-1">
						<textarea
							id="notes"
							name="notes"
							rows="4"
							class="w-full rounded-input border border-stone bg-white p-3 text-body-sm text-ink placeholder:text-secondary/60 focus:border-ink focus:outline-none resize-none leading-relaxed"
							placeholder={t('customers.form.notesPlaceholder')}
						>{values?.notes ?? ''}</textarea>
					</div>
				</div>
			</div>
		</div>

		<!-- Right: Actions & Operational Guide -->
		<div class="lg:col-span-4 space-y-md">
			<!-- Submit & Actions Card -->
			<div class="rounded-2xl border border-stone bg-white p-5 shadow-2xs space-y-3">
				<button
					type="submit"
					disabled={isSaving}
					class="w-full h-10 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer inline-flex items-center justify-center gap-2 disabled:opacity-60"
				>
					{#if isSaving}
						<span class="inline-block w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
						<span>{t('customers.form.saving')}</span>
					{:else}
						<span class="material-symbols-outlined text-[18px]">how_to_reg</span>
						<span>{t('customers.form.saveCustomer')}</span>
					{/if}
				</button>

				<a
					href="/app/customers"
					class="w-full h-10 rounded-button border border-stone bg-white text-ink hover:bg-ash font-medium text-body-sm transition-all shadow-2xs inline-flex items-center justify-center"
				>
					{t('common.cancel')}
				</a>

				<p class="text-[11px] text-secondary text-center font-mono pt-1">
					{t('customers.form.shortcutHint')}
				</p>
			</div>

			<!-- Intake Best Practices Guide -->
			<div class="rounded-2xl border border-stone/80 bg-surface-container-low/50 p-5 shadow-2xs space-y-3">
				<div class="flex items-center gap-2 text-ink">
					<span class="material-symbols-outlined text-[19px] text-amber-600">tips_and_updates</span>
					<h4 class="font-bold text-xs uppercase tracking-wider font-label-caps">
						{t('customers.form.tipTitle')}
					</h4>
				</div>

				<ul class="space-y-2 text-xs text-secondary leading-relaxed">
					<li class="flex items-start gap-2">
						<span class="material-symbols-outlined text-[15px] text-emerald-600 shrink-0 mt-0.5">check_circle</span>
						<span>{t('customers.form.tip1')}</span>
					</li>
					<li class="flex items-start gap-2">
						<span class="material-symbols-outlined text-[15px] text-action-blue shrink-0 mt-0.5">devices</span>
						<span>{t('customers.form.tip2')}</span>
					</li>
					<li class="flex items-start gap-2">
						<span class="material-symbols-outlined text-[15px] text-secondary shrink-0 mt-0.5">lock</span>
						<span>{t('customers.form.tip3')}</span>
					</li>
				</ul>
			</div>
		</div>
	</form>
</div>
