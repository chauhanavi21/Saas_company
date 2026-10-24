import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

export type Role = 'Admin' | 'Member';

export interface TeamMember {
  id: string;
  email: string;
  fullName: string;
  role: Role;
}

export interface Invitation {
  id: string;
  email: string;
  role: Role;
  expiresAtUtc: string;
  isPending: boolean;
}

export interface CreatedInvitation {
  invitation: Invitation;
  token: string;
}

export interface InvitationPreview {
  companyName: string;
  email: string;
  role: Role;
}

@Injectable({ providedIn: 'root' })
export class TeamApi {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  list() {
    return this.http.get<TeamMember[]>(`${this.base}/team`);
  }
  changeRole(id: string, role: Role) {
    return this.http.put<TeamMember>(`${this.base}/team/${id}/role`, { role });
  }
  remove(id: string) {
    return this.http.delete<void>(`${this.base}/team/${id}`);
  }

  pendingInvitations() {
    return this.http.get<Invitation[]>(`${this.base}/invitations`);
  }
  invite(email: string, role: Role) {
    return this.http.post<CreatedInvitation>(`${this.base}/invitations`, { email, role });
  }
  revokeInvitation(id: string) {
    return this.http.delete<void>(`${this.base}/invitations/${id}`);
  }
  previewInvitation(token: string) {
    return this.http.get<InvitationPreview>(`${this.base}/invitations/preview`, { params: { token } });
  }
}
