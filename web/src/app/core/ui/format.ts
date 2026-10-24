const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/** "just now", "5 minutes ago", "3 days ago" — falls back to a date after a month. */
export function timeAgo(iso: string, now = Date.now()): string {
  const diff = now - new Date(iso).getTime();
  if (Number.isNaN(diff)) return '';
  if (diff < MINUTE) return 'just now';
  if (diff < HOUR) return plural(Math.floor(diff / MINUTE), 'minute');
  if (diff < DAY) return plural(Math.floor(diff / HOUR), 'hour');
  if (diff < 30 * DAY) return plural(Math.floor(diff / DAY), 'day');
  return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
}

const plural = (n: number, unit: string) => `${n} ${unit}${n === 1 ? '' : 's'} ago`;

/** "Oct 24" for a YYYY-MM-DD date, without the off-by-one a timezone shift would cause. */
export function shortDate(date: string): string {
  const [y, m, d] = date.split('-').map(Number);
  return new Date(y!, m! - 1, d!).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

/** A readable sentence for an activity entry, e.g. "Alice created project Website". */
export function describeActivity(a: { actorName: string; action: string; summary: string }): string {
  const who = a.actorName;
  switch (a.action) {
    case 'company.created': return `${who} created the workspace ${a.summary}`;
    case 'company.renamed': return `${who} renamed the company to ${a.summary}`;
    case 'project.created': return `${who} created project ${a.summary}`;
    case 'project.updated': return `${who} updated project ${a.summary}`;
    case 'project.archived': return `${who} archived project ${a.summary}`;
    case 'project.unarchived': return `${who} restored project ${a.summary}`;
    case 'project.deleted': return `${who} deleted project ${a.summary}`;
    case 'task.created': return `${who} added task ${a.summary}`;
    case 'task.moved': return `${who} moved ${a.summary}`;
    case 'task.assigned': return `${who} reassigned ${a.summary}`;
    case 'task.deleted': return `${who} deleted task ${a.summary}`;
    case 'task.commented': return `${who} commented on ${a.summary}`;
    case 'member.role_changed': return `${who} changed a role: ${a.summary}`;
    case 'member.removed': return `${who} removed ${a.summary} from the team`;
    case 'invitation.created': return `${who} invited ${a.summary}`;
    case 'invitation.revoked': return `${who} revoked the invite for ${a.summary}`;
    case 'invitation.accepted': return a.summary;
    default: return `${who} ${a.action} ${a.summary}`.trim();
  }
}
