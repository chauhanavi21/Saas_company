import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

export type TaskStatus = 'Todo' | 'InProgress' | 'Done';
export type TaskPriority = 'Low' | 'Medium' | 'High';

export interface Project {
  id: string;
  name: string;
  description: string | null;
  isArchived: boolean;
  taskCount: number;
  doneCount: number;
  createdAtUtc: string;
}

export interface ProjectTask {
  id: string;
  projectId: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  assigneeUserId: string | null;
  dueDate: string | null;
  commentCount: number;
  isOverdue: boolean;
}

export interface TaskComment {
  id: string;
  authorUserId: string;
  authorName: string;
  body: string;
  createdAtUtc: string;
  canDelete: boolean;
}

export interface TaskFilter {
  search?: string;
  status?: TaskStatus | '';
  assignee?: string | '';
  mine?: boolean;
}

export interface TaskInput {
  title: string;
  description: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  assigneeUserId: string | null;
  dueDate: string | null;
}

@Injectable({ providedIn: 'root' })
export class ProjectsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/projects`;

  list(includeArchived = false, search = '') {
    let params = new HttpParams().set('includeArchived', includeArchived);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<Project[]>(this.base, { params });
  }
  get(id: string) {
    return this.http.get<Project>(`${this.base}/${id}`);
  }
  create(name: string, description: string | null) {
    return this.http.post<Project>(this.base, { name, description });
  }
  update(id: string, name: string, description: string | null) {
    return this.http.put<Project>(`${this.base}/${id}`, { name, description });
  }
  archive(id: string) {
    return this.http.post<void>(`${this.base}/${id}/archive`, {});
  }
  unarchive(id: string) {
    return this.http.post<void>(`${this.base}/${id}/unarchive`, {});
  }
  delete(id: string) {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  tasks(projectId: string, filter: TaskFilter = {}) {
    let params = new HttpParams();
    if (filter.search?.trim()) params = params.set('search', filter.search.trim());
    if (filter.status) params = params.set('status', filter.status);
    if (filter.assignee) params = params.set('assignee', filter.assignee);
    if (filter.mine) params = params.set('mine', true);
    return this.http.get<ProjectTask[]>(`${this.base}/${projectId}/tasks`, { params });
  }
  createTask(projectId: string, input: Partial<TaskInput> & { title: string }) {
    return this.http.post<ProjectTask>(`${this.base}/${projectId}/tasks`, {
      title: input.title,
      description: input.description ?? null,
      assigneeUserId: input.assigneeUserId ?? null,
      dueDate: input.dueDate ?? null,
      priority: input.priority ?? 'Medium',
    });
  }
  updateTask(projectId: string, taskId: string, input: TaskInput) {
    return this.http.put<ProjectTask>(`${this.base}/${projectId}/tasks/${taskId}`, input);
  }
  deleteTask(projectId: string, taskId: string) {
    return this.http.delete<void>(`${this.base}/${projectId}/tasks/${taskId}`);
  }

  comments(projectId: string, taskId: string) {
    return this.http.get<TaskComment[]>(`${this.base}/${projectId}/tasks/${taskId}/comments`);
  }
  addComment(projectId: string, taskId: string, body: string) {
    return this.http.post<TaskComment>(`${this.base}/${projectId}/tasks/${taskId}/comments`, { body });
  }
  deleteComment(projectId: string, taskId: string, commentId: string) {
    return this.http.delete<void>(`${this.base}/${projectId}/tasks/${taskId}/comments/${commentId}`);
  }
}
