import { Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home.component';
import { BankAccountsComponent } from './pages/bank-accounts/bank-accounts.component';
import { BankAccountDetailComponent } from './pages/bank-account-detail/bank-account-detail.component';

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
    path: 'accounts/:id',
    component: BankAccountDetailComponent
  },
  {
    path: '**',
    redirectTo: ''
  }
];
