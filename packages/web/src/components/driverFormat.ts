/** "Jan Kowalski" from any object carrying first/last name fields. */
export function fullName(person: { firstName: string; lastName: string }): string {
  return `${person.firstName} ${person.lastName}`
}
