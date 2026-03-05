import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { UserManagementService, TeamMember, InviteUserRequest } from '../../services/user-management.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../services/toast.service';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-user-management',
  templateUrl: './user-management.component.html',
  styleUrls: ['./user-management.component.css']
})
export class UserManagementComponent implements OnInit, OnDestroy {
  teamMembers: TeamMember[] = [];
  isLoading = true;
  loadError = '';
  
  // Invite form
  showInviteForm = false;
  inviteData: InviteUserRequest = {
    email: '',
    firstName: '',
    lastName: '',
    role: '2' // Default: Auditor
  };
  inviting = false;
  
  // Stats
  activeCount = 0;
  adminCount = 0;
  auditorCount = 0;
  managerCount = 0;
  
  // Current user
  isAdmin = false;
  
  roles = [
    { value: '1', label: 'Admin' },
    { value: '2', label: 'Auditor' },
    { value: '3', label: 'Manager' },
    { value: '4', label: 'Viewer' }
  ];
  
  private subscriptions = new Subscription();
  
  constructor(
    private userService: UserManagementService,
    private authService: AuthService,
    private toastService: ToastService,
    private dialog: MatDialog
  ) {}
  
  ngOnInit(): void {
    this.isAdmin = this.authService.isAdmin() || this.authService.isManager();
    this.loadTeam();
  }
  
  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }
  
  loadTeam(): void {
    this.isLoading = true;
    this.loadError = '';
    
    const sub = this.userService.getTeamMembers().subscribe({
      next: (members) => {
        this.teamMembers = members;
        this.computeStats();
        this.isLoading = false;
      },
      error: (err) => {
        this.loadError = 'Failed to load team members.';
        this.isLoading = false;
      }
    });
    
    this.subscriptions.add(sub);
  }
  
  computeStats(): void {
    this.activeCount = this.teamMembers.filter(m => m.isActive).length;
    this.adminCount = this.teamMembers.filter(m => m.role === '1').length;
    this.auditorCount = this.teamMembers.filter(m => m.role === '2').length;
    this.managerCount = this.teamMembers.filter(m => m.role === '3').length;
  }
  
  toggleInviteForm(): void {
    this.showInviteForm = !this.showInviteForm;
    if (this.showInviteForm) {
      this.inviteData = { email: '', firstName: '', lastName: '', role: '2' };
    }
  }
  
  submitInvite(): void {
    if (!this.inviteData.email || !this.inviteData.firstName || !this.inviteData.lastName) {
      this.toastService.warning('Please fill in all required fields');
      return;
    }
    
    this.inviting = true;
    this.subscriptions.add(
      this.userService.inviteUser(this.inviteData).subscribe({
        next: (member) => {
          this.teamMembers.push(member);
          this.computeStats();
          this.showInviteForm = false;
          this.inviting = false;
          this.toastService.success(`Invitation sent to ${member.email}`);
        },
        error: (err) => {
          this.toastService.error('Failed to send invitation');
          this.inviting = false;
        }
      })
    );
  }
  
  changeRole(member: TeamMember, newRole: string): void {
    this.subscriptions.add(
      this.userService.updateUserRole(member.id, { role: newRole }).subscribe({
        next: (updated) => {
          member.role = updated.role;
          member.roleName = updated.roleName;
          this.computeStats();
          this.toastService.success(`${member.fullName}'s role updated`);
        },
        error: () => this.toastService.error('Failed to update role')
      })
    );
  }
  
  toggleUserStatus(member: TeamMember): void {
    const action = member.isActive ? 'deactivate' : 'activate';
    
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: `${action === 'deactivate' ? 'Deactivate' : 'Activate'} User`,
        message: `Are you sure you want to ${action} ${member.fullName}?`,
        icon: action === 'deactivate' ? 'person_off' : 'person',
        confirmText: action === 'deactivate' ? 'Deactivate' : 'Activate',
        confirmColor: action === 'deactivate' ? 'warn' : 'primary'
      }
    });
    
    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) return;
      
      const obs = member.isActive 
        ? this.userService.deactivateUser(member.id)
        : this.userService.activateUser(member.id);
        
      this.subscriptions.add(
        obs.subscribe({
          next: () => {
            member.isActive = !member.isActive;
            this.computeStats();
            this.toastService.success(`${member.fullName} ${action}d`);
          },
          error: () => this.toastService.error(`Failed to ${action} user`)
        })
      );
    });
  }
  
  getRoleLabel(role: string): string {
    const found = this.roles.find(r => r.value === role);
    return found ? found.label : 'User';
  }
  
  getRoleBadgeClass(role: string): string {
    switch (role) {
      case '1': return 'badge-admin';
      case '2': return 'badge-auditor';
      case '3': return 'badge-manager';
      default: return 'badge-viewer';
    }
  }
  
  getUserInitials(member: TeamMember): string {
    return ((member.firstName?.[0] || '') + (member.lastName?.[0] || '')).toUpperCase() || '?';
  }
}
