import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface MyTask {
  id: string;
  projectId: string;
  projectName: string;
  title: string;
  status: string;
  priority: string;
  dueDate: string | null;
  isOverdue: boolean;
}

export interface Dashboard {
  activeProjects: number;
  teamMembers: number;
  openTasks: number;
  overdueTasks: number;
  completedThisWeek: number;
  tasksByStatus: { status: string; count: number }[];
  myTasks: MyTask[];
}

export interface Activity {
  id: string;
  actorName: string;
  action: string;
  entityType: string;
  entityId: string | null;
  summary: string;
  atUtc: string;
}

export interface Profile {
  id: string;
  email: string;
  fullName: string;
  role: string;
  companyName: string;
  memberSinceUtc: string;
}

@Injectable({ providedIn: 'root' })
export class InsightsApi {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  dashboard() {
    return this.http.get<Dashboard>(`${this.base}/dashboard`);
  }
  activity(take = 20) {
    return this.http.get<Activity[]>(`${this.base}/activity`, { params: { take } });
  }
  profile() {
    return this.http.get<Profile>(`${this.base}/account`);
  }
  updateProfile(fullName: string) {
    return this.http.put<Profile>(`${this.base}/account/profile`, { fullName });
  }
  updateCompany(name: string) {
    return this.http.put<Profile>(`${this.base}/account/company`, { name });
  }
}
