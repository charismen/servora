import type { OperatingMode, TerminologyConfig, WorkspaceFeatureConfig } from './auth';

export type CustomFieldScope = 'Asset' | 'Ticket';
export type CustomFieldType = 'Text' | 'Number' | 'Select' | 'Boolean' | 'Date' | 'Multiline';
export type InspectionItemStatus = 'Unchecked' | 'Pass' | 'Issue' | 'NotApplicable';

export type BlueprintStatusSeed = {
	name: string;
	color: string;
	sortOrder: number;
	isInitial: boolean;
	isBlocked: boolean;
	isFinal: boolean;
	isCancelled: boolean;
};

export type BlueprintFieldSeed = {
	scope: CustomFieldScope;
	fieldKey: string;
	labelEn: string;
	labelId: string;
	fieldType: CustomFieldType;
	options?: string[] | null;
	isRequired: boolean;
	isSearchable: boolean;
	showOnIntake: boolean;
	showOnDetail: boolean;
	showOnPrint: boolean;
	showOnPublicTracking: boolean;
};

export type BlueprintChecklistSeed = {
	itemKey: string;
	labelEn: string;
	labelId: string;
	category: string;
	isRequired: boolean;
};

export type BlueprintTemplateSeed = {
	name: string;
	category: string;
	description: string;
	estimatedPrice: number;
	estimatedDurationMinutes: number;
	warrantyDays: number;
	checklistItems: string[];
};

export type IndustryBlueprintDefinition = {
	key: string;
	nameEn: string;
	nameId: string;
	descriptionEn: string;
	descriptionId: string;
	recommendedMode: OperatingMode;
	defaultMeterUnit: string;
	defaultTicketPrefix: string;
	defaultAssetCategory: string;
	terminology: TerminologyConfig;
	featureConfig: WorkspaceFeatureConfig;
	statuses: BlueprintStatusSeed[];
	fields: BlueprintFieldSeed[];
	checklists: BlueprintChecklistSeed[];
	serviceTemplates: BlueprintTemplateSeed[];
};

export type CustomFieldDefinition = {
	id: number;
	scope: CustomFieldScope;
	fieldKey: string;
	labelEn: string;
	labelId: string;
	fieldType: CustomFieldType;
	options: string[];
	isRequired: boolean;
	isSearchable: boolean;
	showOnIntake: boolean;
	showOnDetail: boolean;
	showOnPrint: boolean;
	showOnPublicTracking: boolean;
	sortOrder: number;
	isActive: boolean;
};

export type InspectionChecklistTemplate = {
	id: number;
	itemKey: string;
	labelEn: string;
	labelId: string;
	category: string | null;
	sortOrder: number;
	isRequired: boolean;
	isActive: boolean;
};

export type WorkspaceBlueprintConfig = {
	workspaceId: number;
	workspaceName: string;
	operatingMode: OperatingMode;
	industryBlueprintKey: string;
	setupCompleted: boolean;
	terminology: TerminologyConfig;
	featureConfig: WorkspaceFeatureConfig;
	assetFields: CustomFieldDefinition[];
	ticketFields: CustomFieldDefinition[];
	inspectionChecklists: InspectionChecklistTemplate[];
};

export type FieldValueDto = {
	fieldDefinitionId: number;
	fieldKey: string;
	labelEn: string;
	labelId: string;
	fieldType: CustomFieldType;
	value: string | null;
	showOnPrint: boolean;
	showOnPublicTracking: boolean;
};

export type TicketInspectionDto = {
	id: number;
	checklistTemplateId: number | null;
	label: string;
	status: InspectionItemStatus;
	notes: string | null;
};
