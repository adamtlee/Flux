export interface BankAccount {
  id: string;
  accountNumber: string;
  accountHolder: string;
  balance: number;
  accountType: string;
  createdAt: Date;
  updatedAt: Date;
}
