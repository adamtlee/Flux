export interface BankAccount {
  id: string;
  accountName: string;
  owner: string;
  balance: number;
  type: AccountType;
  createdAt: string;
  updatedAt: string;
}

export enum AccountType {
  Checking = 0,
  Savings = 1,
  CreditCard = 2
}
