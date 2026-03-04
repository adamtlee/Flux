import { Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home.component';
import { BankAccountsComponent } from './pages/bank-accounts/bank-accounts.component';

export const routes: Routes = [
  {
    path: '',
    component: HomeComponent
  },
  {
    path: 'accounts',
    component: BankAccountsComponent
  },
  {
    path: '**',
    redirectTo: ''
  }
];
