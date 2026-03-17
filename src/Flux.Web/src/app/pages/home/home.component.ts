import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  features = [
    {
      icon: '💼',
      title: 'Account Management',
      description: 'Keep your accounts organized in one clean workspace built for everyday use.'
    },
    {
      icon: '💰',
      title: 'Transaction Tracking',
      description: 'Review activity clearly and stay on top of incoming and outgoing transactions.'
    },
    {
      icon: '📊',
      title: 'Analytics & Insights',
      description: 'See the patterns behind your finances with simple insights that are easy to understand.'
    },
    {
      icon: '🏠',
      title: 'Own Your Data',
      description: 'Install Flux locally and keep your financial data under your control without relying on third-party services.'
    }
  ];
}
