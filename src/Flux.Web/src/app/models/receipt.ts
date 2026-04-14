export interface ReceiptItem {
  id?: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal?: number;
}

export interface Receipt {
  id: number;
  ownerUserId: string;
  ownerUsername: string;
  accountId?: number | null;
  merchantName: string;
  purchasedAtUtc: string;
  totalAmount: number;
  currencyCode: string;
  notes?: string | null;
  items: ReceiptItem[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateReceiptRequest {
  accountId?: number | null;
  merchantName: string;
  purchasedAtUtc: string;
  totalAmount: number;
  currencyCode: string;
  notes?: string | null;
  items: ReceiptItem[];
}

export interface UpdateReceiptRequest extends CreateReceiptRequest {}
