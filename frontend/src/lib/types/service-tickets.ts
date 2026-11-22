import type { TerminologyConfig, WorkspaceFeatureConfig } from './auth';
import type { FieldValueDto, TicketInspectionDto } from './blueprints';
import type { ServiceLocationType } from './maintenance';

export type WorkItemType = 'Service' | 'NonStockPart' | 'Fee';
export type WorkItemStatus = 'PendingApproval' | 'Approved' | 'Rejected' | 'Cancelled';
export type PaymentEntryType = 'Payment' | 'Refund' | 'BillingUpdate';

export interface TicketTransition {
	statusId: number;
	name: string;
	color: string;
	requiresReason: boolean;
	requiresApproval: boolean;
	requiresPaymentCompleted: boolean;
}

export interface TicketSummary {
	id: number;
	ticketNumber: string;
	customerName: string;
	customerPhone: string;
	device: string;
	status: string;
	statusColor: string;
	priority: string;
	paymentStatus: string;
	assignedTechnician?: string;
	warrantyUntil?: string;
	warrantyActive: boolean;
	createdAt: string;
	isInternalWorkOrder?: boolean;
	serviceLocationType?: ServiceLocationType | string;
	siteAddressSnapshot?: string | null;
	scheduledVisitStart?: string | null;
	slaDueAt?: string | null;
}

export interface TicketSummaryPage {
	items: TicketSummary[];
	totalCount: number;
	page: number;
	pageSize: number;
}

export interface TicketDetail {
	id: number;
	ticketNumber: string;
	customerId: number;
	deviceId: number;
	statusId: number;
	status: string;
	statusColor: string;
	customerName: string;
	customerPhone: string;
	device: string;
	priority: string;
	complaint: string;
	initialDiagnosis?: string;
	physicalConditionNotes?: string;
	accessorySummary?: string;
	photoNotes?: string;
	securityPasscode?: string;
	securityPattern?: string;
	preRepairChecklist?: string;
	diagnosis?: string;
	repairNotes?: string;
	warrantyUntil?: string;
	warrantyTerms?: string;
	warrantyActive: boolean;
	approvalRequired: boolean;
	isPreApproved: boolean;
	paymentStatus: string;
	assignedTechnician?: string;
	assignedTechnicianUserId?: number;
	accessories: TicketAccessory[];
	attachments: TicketAttachment[];
	allowedTransitions: TicketTransition[];
	createdAt: string;
	updatedAt?: string;
	hasPendingApproval?: boolean;
	workItemsSubtotal?: number;
	consumedPartsSubtotal?: number;
	discountAmount?: number;
	totalAmount?: number | null;
	amountPaid?: number;
	remainingBalance?: number;
	overpaidAmount?: number;
	isInternalWorkOrder?: boolean;
	departmentCode?: string | null;
	costCenter?: string | null;
	slaDueAt?: string | null;
	serviceLocationType?: ServiceLocationType | string;
	siteLocationId?: number | null;
	siteAddressSnapshot?: string | null;
	scheduledVisitStart?: string | null;
	scheduledVisitEnd?: string | null;
	parentTicketId?: number | null;
	linkedAssets?: { serviceAssetId: number; brand: string; model: string; primaryIdentifier?: string | null; notes?: string | null }[];
	customFields?: FieldValueDto[];
	assetCustomFields?: FieldValueDto[];
	inspections?: TicketInspectionDto[];
	serviceResourceId?: number | null;
	serviceResourceName?: string | null;
	dispatchStatus?: string;
	visitStartedAt?: string | null;
	visitCompletedAt?: string | null;
	dispatchNotes?: string | null;
}

export interface TicketAccessory {
	id: number;
	name: string;
	quantity: number;
	notes?: string;
}

export interface TicketAttachment {
	id: number;
	fileName: string;
	mimeType: string;
	fileSize: number;
	attachmentType: string;
	isPublicVisible: boolean;
	createdAt: string;
}

export interface TicketHistory {
	id: number;
	eventType: string;
	fromStatus?: string;
	toStatus?: string;
	reason?: string;
	note?: string;
	isPublicVisible: boolean;
	createdAt: string;
}

export interface TicketApproval {
	id: number;
	title: string;
	description: string;
	estimatedAmount?: number;
	status: string;
	resolutionNote?: string;
	createdAt: string;
	resolvedAt?: string;
}

export interface TicketWorkItem {
	id: number;
	approvalRequestId?: number | null;
	itemType: WorkItemType | string;
	title: string;
	description?: string | null;
	quantity: number;
	unitPrice: number;
	lineTotal: number;
	status: WorkItemStatus | string;
	isCompleted: boolean;
	createdAt: string;
	updatedAt?: string | null;
}

export interface PaymentTransaction {
	id: number;
	entryType: PaymentEntryType | string;
	status: string;
	transactionAmount: number;
	amountPaid: number;
	totalAmount?: number | null;
	method?: string | null;
	referenceNumber?: string | null;
	notes?: string | null;
	updatedByName?: string | null;
	recordedAt: string;
	createdAt: string;
	isVoided: boolean;
	voidedAt?: string | null;
	voidedByName?: string | null;
	voidReason?: string | null;
}

export interface TicketPayment {
	status: string;
	amountPaid: number;
	totalAmount?: number | null;
	method?: string | null;
	referenceNumber?: string | null;
	notes?: string | null;
	lastUpdatedAt?: string | null;
	calculatedTotalAmount?: number;
	discountAmount?: number;
	isTotalManuallyOverridden?: boolean;
	remainingBalance?: number;
	overpaidAmount?: number;
	workItemsSubtotal?: number;
	consumedPartsSubtotal?: number;
	transactions?: PaymentTransaction[];
}

export interface ServiceHistoryTicket {
	id: number;
	ticketNumber: string;
	device: string;
	status: string;
	statusColor: string;
	priority: string;
	paymentStatus: string;
	createdAt: string;
	warrantyUntil?: string;
	warrantyActive: boolean;
}

export interface RepeatCustomerSummary {
	customerId: number;
	totalTickets: number;
	activeTickets: number;
	completedTickets: number;
	lastTicketAt?: string;
}

export interface KanbanTicket {
	id: number;
	ticketNumber: string;
	customerName: string;
	device: string;
	complaint: string;
	priority: string;
	paymentStatus: string;
	assignedTechnician?: string;
	createdAt: string;
	waitingApproval: boolean;
	unpaid: boolean;
	blocked: boolean;
	overdue: boolean;
}

export interface KanbanColumn {
	statusId: number;
	name: string;
	color: string;
	sortOrder: number;
	tickets: KanbanTicket[];
	isFinal?: boolean;
	isCancelled?: boolean;
}

export interface DashboardSummary {
	openTickets: number;
	waitingApproval: number;
	unpaidTickets: number;
	blockedTickets: number;
	overdueTickets: number;
	completedToday: number;
}

export interface GeneratedWhatsAppMessage {
	templateKey: string;
	locale: string;
	phoneNumber: string;
	message: string;
	whatsAppUrl: string;
}

export interface ReceiptWorkItem {
	itemType: string;
	title: string;
	description?: string | null;
	quantity: number;
	unitPrice: number;
	lineTotal: number;
}

export interface ReceiptPart {
	sku: string;
	itemName: string;
	unit: string;
	netQuantity: number;
	unitPrice: number;
	lineTotal: number;
}

export interface TicketReceipt {
	id: number;
	ticketNumber: string;
	customerName: string;
	customerPhone: string;
	device: string;
	complaint: string;
	initialDiagnosis?: string;
	physicalConditionNotes?: string;
	accessorySummary?: string;
	photoNotes?: string;
	securityPasscode?: string;
	securityPattern?: string;
	priority: string;
	status: string;
	createdAt: string;
	publicTrackingToken: string;
	shopName: string;
	logoUrl?: string | null;
	phoneNumber: string;
	whatsAppNumber: string;
	address: string;
	receiptFooter: string;
	termsAndConditions: string;
	showPhysicalCondition: boolean;
	showAccessories: boolean;
	showPhotoNotes: boolean;
	showQrCode: boolean;
	showPhoneNumber: boolean;
	showPaymentSummary: boolean;
	paymentStatus: string;
	currencyCode: 'IDR' | 'USD' | string;
	workItemsSubtotal: number;
	consumedPartsSubtotal: number;
	discountAmount: number;
	totalAmount?: number | null;
	amountPaid: number;
	remainingBalance: number;
	overpaidAmount: number;
	workItems: ReceiptWorkItem[];
	consumedParts: ReceiptPart[];
	serviceLocationType?: ServiceLocationType | string;
	siteAddressSnapshot?: string | null;
	scheduledVisitStart?: string | null;
	isInternalWorkOrder?: boolean;
	printCustomFields?: FieldValueDto[];
	terminology?: TerminologyConfig | null;
	featureConfig?: WorkspaceFeatureConfig | null;
	printInspections?: TicketInspectionDto[];
	showPaymentQrCode?: boolean;
	activePaymentUrl?: string | null;
}
