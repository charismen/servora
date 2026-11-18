<script lang="ts">
	import { untrack } from 'svelte';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { createTranslator, resolveTerminology } from '$lib/i18n';
	import { handlePhoneInput, handlePhonePaste } from '$lib/utils/phone';
	import type { PageData, ActionData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const terms = $derived(resolveTerminology(data.user?.workspace?.terminology, data.locale));
	let customer = $derived(data.customer);
	let values = $derived(form?.values);
	let requesterType = $state<string>(untrack(() => values?.requesterType ?? customer.requesterType ?? 'Customer'));

	let isSaving = $state(false);
</script>

<div class="max-w-4xl space-y-lg">
	<PageHeader
		items={[
			{ label: t('common.console'), href: '/app' },
			{ label: terms.requesterPlural, href: '/app/customers' },
			{ label: customer.fullName, href: `/app/customers/${customer.id}` },
			{ label: t('customers.form.editProfile') }
		]}
		title={`${t('customers.form.editProfile')} — ${customer.fullName}`}
	/>

	<!-- Form Container -->
	<form 
		class="space-y-lg" 
		method="POST"
		onsubmit={() => isSaving = true}
	>
		<input type="hidden" name="updatedAt" value={values?.updatedAt ?? customer.updatedAt ?? ''} />

		<!-- Section 1: Personal Information -->
		<section class="bg-surface-container-lowest border border-stone rounded-lg shadow-sm overflow-hidden">
			<div class="p-card-padding border-b border-stone bg-ash">
				<h3 class="font-subheading text-subheading font-bold text-on-surface">{t('customers.form.personalInfo')}</h3>
			</div>
			<div class="p-card-padding grid grid-cols-1 md:grid-cols-2 gap-lg">
				<div class="space-y-base md:col-span-2">
					<label for="requesterType" class="block text-body-sm font-medium text-secondary">{t('customers.form.requesterType')}</label>
					<select
						id="requesterType"
						name="requesterType"
						bind:value={requesterType}
						class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink bg-white focus:border-ink focus:outline-none"
					>
						<option value="Customer">{t('customers.type.Customer')}</option>
						<option value="Department">{t('customers.type.Department')}</option>
						<option value="InternalEmployee">{t('customers.type.InternalEmployee')}</option>
						<option value="Branch">{t('customers.type.Branch')}</option>
					</select>
				</div>
				<div class="space-y-base">
					<label for="fullName" class="block text-body-sm font-medium text-secondary">{t('customers.form.fullName')} <span class="text-error">*</span></label>
					<input 
						id="fullName"
						name="fullName"
						value={values?.fullName ?? customer.fullName}
						class="w-full border border-slate rounded-[7px] p-sm text-body-sm focus:ring-2 focus:ring-focus-blue focus:outline-none bg-background text-on-surface" 
						placeholder={t('customers.form.fullNamePlaceholder')}
						required 
						type="text"
					/>
				</div>
				<div class="space-y-base">
					<label for="phoneNumber" class="block text-body-sm font-medium text-secondary">
						{t('customers.form.phoneNumber')}
						{#if requesterType === 'Customer'}<span class="text-error">*</span>{/if}
					</label>
					<input 
						id="phoneNumber"
						name="phoneNumber"
						value={values?.phoneNumber ?? customer.phoneNumber}
						class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink font-mono focus:border-ink focus:outline-none bg-white" 
						placeholder={requesterType === 'Customer' ? t('customers.form.phonePlaceholder') : t('customers.form.phoneOptionalInternal')}
						required={requesterType === 'Customer'}
						type="tel"
						oninput={handlePhoneInput}
						onpaste={handlePhonePaste}
					/>
				</div>
				{#if requesterType === 'Customer'}
					<div class="space-y-base md:col-span-2">
						<label for="companyName" class="block text-body-sm font-medium text-secondary">{t('customers.form.companyName')}</label>
						<input
							id="companyName"
							name="companyName"
							value={values?.companyName ?? customer.companyName ?? ''}
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink bg-white focus:border-ink focus:outline-none"
							placeholder={t('customers.form.companyNamePlaceholder')}
							type="text"
						/>
					</div>
				{:else}
					<div class="space-y-base">
						<label for="departmentName" class="block text-body-sm font-medium text-secondary">{t('customers.form.departmentName')}</label>
						<input
							id="departmentName"
							name="departmentName"
							value={values?.departmentName ?? customer.departmentName ?? ''}
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink bg-white focus:border-ink focus:outline-none"
							placeholder={t('customers.form.departmentNamePlaceholder')}
							type="text"
						/>
					</div>
					<div class="space-y-base">
						<label for="departmentCode" class="block text-body-sm font-medium text-secondary">{t('customers.form.departmentCode')}</label>
						<input
							id="departmentCode"
							name="departmentCode"
							value={values?.departmentCode ?? customer.departmentCode ?? ''}
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink bg-white focus:border-ink focus:outline-none"
							placeholder={t('customers.form.departmentCodePlaceholder')}
							type="text"
						/>
					</div>
					<div class="space-y-base">
						<label for="costCenter" class="block text-body-sm font-medium text-secondary">{t('customers.form.costCenter')}</label>
						<input
							id="costCenter"
							name="costCenter"
							value={values?.costCenter ?? customer.costCenter ?? ''}
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink bg-white focus:border-ink focus:outline-none"
							placeholder={t('customers.form.costCenterPlaceholder')}
							type="text"
						/>
					</div>
					<div class="space-y-base">
						<label for="employeeId" class="block text-body-sm font-medium text-secondary">{t('customers.form.employeeId')}</label>
						<input
							id="employeeId"
							name="employeeId"
							value={values?.employeeId ?? customer.employeeId ?? ''}
							class="w-full border border-stone rounded-input px-3 py-2 text-body-sm text-ink bg-white focus:border-ink focus:outline-none"
							placeholder={t('customers.form.employeeIdPlaceholder')}
							type="text"
						/>
					</div>
				{/if}
				<div class="space-y-base md:col-span-2">
					<label for="email" class="block text-body-sm font-medium text-secondary">{t('customers.form.email')}</label>
					<input 
						id="email"
						name="email"
						value={values?.email ?? customer.email ?? ''}
						class="w-full border border-slate rounded-[7px] p-sm text-body-sm focus:ring-2 focus:ring-focus-blue focus:outline-none bg-background text-on-surface" 
						placeholder={t('customers.form.emailPlaceholder')} 
						type="email"
					/>
				</div>
			</div>
		</section>

		<!-- Section 2: Address & Location -->
		<section class="bg-surface-container-lowest border border-stone rounded-lg shadow-sm overflow-hidden">
			<div class="p-card-padding border-b border-stone bg-ash">
				<h3 class="font-subheading text-subheading font-bold text-on-surface">{t('customers.form.addressSection')}</h3>
			</div>
			<div class="p-card-padding space-y-lg">
				<div class="space-y-base">
					<label for="address" class="block text-body-sm font-medium text-secondary">{t('customers.form.address')}</label>
					<input 
						id="address"
						name="address"
						value={values?.address ?? customer.address ?? ''}
						class="w-full border border-slate rounded-[7px] p-sm text-body-sm focus:ring-2 focus:ring-focus-blue focus:outline-none bg-background text-on-surface" 
						placeholder={t('customers.form.addressPlaceholder')}
						type="text"
					/>
				</div>
			</div>
		</section>

		<!-- Section 3: Notes -->
		<section class="bg-surface-container-lowest border border-stone rounded-lg shadow-sm overflow-hidden">
			<div class="p-card-padding border-b border-stone bg-ash">
				<h3 class="font-subheading text-subheading font-bold text-on-surface">{t('customers.form.internalNotes')}</h3>
			</div>
			<div class="p-card-padding space-y-lg">
				<div class="space-y-base">
					<label for="notes" class="block text-body-sm font-medium text-secondary">{t('customers.form.internalNotes')}</label>
					<textarea 
						id="notes"
						name="notes"
						class="w-full border border-slate rounded-[7px] p-sm text-body-sm focus:ring-2 focus:ring-focus-blue focus:outline-none bg-background text-on-surface resize-none" 
						placeholder={t('customers.form.notesPlaceholder')}
						rows="4"
					>{values?.notes ?? customer.notes ?? ''}</textarea>
				</div>
			</div>
		</section>

		<!-- Footer Actions -->
		<div class="flex items-center justify-end gap-md pt-lg mb-xxl border-t border-stone">
			<a 
				href="/app/customers/{customer.id}" 
				class="px-xl py-button-y bg-white border border-slate text-primary font-medium rounded-[10px] text-body-sm hover:bg-ash transition-colors inline-flex items-center"
			>
				{t('common.cancel')}
			</a>
			<button 
				class="px-xl py-button-y font-medium rounded-[10px] text-body-sm shadow-md bg-primary text-white hover:opacity-90 transition-opacity disabled:opacity-50 cursor-pointer" 
				type="submit"
				disabled={isSaving}
			>
				{#if isSaving}
					{t('customers.form.saving')}
				{:else}
					{t('customers.form.saveChanges')}
				{/if}
			</button>
		</div>
	</form>
</div>
