import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { IsActiveMatchOptions, Params, RouterLink, RouterLinkActive } from '@angular/router';
import { ApplicationRole } from '../../services/auth.service';

interface SidebarNavItem {
  id: string;
  label: string;
  route: string;
  queryParams?: Params;
  requiredRoles?: ApplicationRole[];
  comingSoon?: boolean;
}

@Component({
  selector: 'app-sidebar',
  imports: [CommonModule, RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent {
  private static readonly exactRouteAndQueryMatch: IsActiveMatchOptions = {
    paths: 'exact',
    queryParams: 'exact',
    matrixParams: 'ignored',
    fragment: 'ignored'
  };

  private static readonly exactPathMatch: IsActiveMatchOptions = {
    paths: 'exact',
    queryParams: 'ignored',
    matrixParams: 'ignored',
    fragment: 'ignored'
  };

  @Input() currentUsername: string | null = null;
  @Input() currentRole: ApplicationRole | null = null;
  @Input() isOpen = false;
  @Input() isCollapsed = false;

  @Output() itemSelected = new EventEmitter<void>();
  @Output() closeRequested = new EventEmitter<void>();

  readonly primaryItems: SidebarNavItem[] = [
    { id: 'home', label: 'Home', route: '/' },
    {
      id: 'accounts',
      label: 'Accounts',
      route: '/accounts',
      queryParams: { tab: 'accounts' }
    },
    {
      id: 'chart-summary',
      label: 'Chart Summary',
      route: '/accounts',
      queryParams: { tab: 'insights', section: 'chart-summary' }
    },
    {
      id: 'receipts',
      label: 'Receipts',
      route: '/accounts',
      queryParams: { tab: 'receipts', section: 'receipts' }
    },
    {
      id: 'subscriptions',
      label: 'Subscriptions',
      route: '/subscriptions'
    },
    {
      id: 'earnings',
      label: 'Earnings',
      route: '/earnings'
    },
    {
      id: 'imports',
      label: 'Imports',
      route: '/accounts',
      queryParams: { tab: 'accounts', section: 'imports' },
      comingSoon: true
    },
    {
      id: 'exports-templates',
      label: 'Exports',
      route: '/accounts',
      queryParams: { tab: 'accounts', section: 'exports' },
      comingSoon: true
    },
    {
      id: 'account-details',
      label: 'Account Details',
      route: '/accounts',
      queryParams: { tab: 'accounts', section: 'account-details' },
      comingSoon: true
    }
  ];

  readonly utilityItems: SidebarNavItem[] = [
    {
      id: 'settings',
      label: 'Settings',
      route: '/accounts',
      comingSoon: true
    },
    {
      id: 'admin-tools',
      label: 'Admin Tools',
      route: '/accounts',
      requiredRoles: ['Administrator'],
      comingSoon: true
    }
  ];

  get visiblePrimaryItems(): SidebarNavItem[] {
    return this.primaryItems.filter((item) => this.canView(item));
  }

  get visibleUtilityItems(): SidebarNavItem[] {
    return this.utilityItems.filter((item) => this.canView(item));
  }

  onNavItemSelected(item: SidebarNavItem): void {
    if (item.comingSoon) {
      return;
    }

    this.itemSelected.emit();
  }

  requestClose(): void {
    this.closeRequested.emit();
  }

  getActiveMatchOptions(item: SidebarNavItem): IsActiveMatchOptions {
    if (item.id === 'home') {
      return SidebarComponent.exactPathMatch;
    }

    // Most sidebar items share /accounts and differ by query params.
    return item.queryParams
      ? SidebarComponent.exactRouteAndQueryMatch
      : SidebarComponent.exactPathMatch;
  }

  private canView(item: SidebarNavItem): boolean {
    if (!item.requiredRoles?.length) {
      return true;
    }

    return this.currentRole !== null && item.requiredRoles.includes(this.currentRole);
  }
}