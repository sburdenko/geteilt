# A data breach: whose data is affected

Worked out in advance rather than on the day. Notifying the supervisory authority and the people
affected follows Articles 33 and 34 GDPR; the deadlines are not repeated here on purpose.

## What is stored where

| What | Where | How it is protected |
|---|---|---|
| IBAN of an account holder | `users.iban` | AES-256-GCM, key `IbanProtection__Key` kept outside the database |
| IBAN of a member without an account | `members.iban` | the same |
| Name, e-mail | `users.display_name`, `users.email`, `members.display_name` | plain text |
| Password hash | `app_users.password_hash` | ASP.NET Core Identity |
| Refresh tokens | `refresh_tokens` | SHA-256 hashes, never the tokens themselves |

Logs never hold an IBAN, only its last four characters.

## First: what exactly leaked

1. **A database dump or backup without the key.** IBANs cannot be read. Names, e-mail addresses
   and group membership are affected.
2. **The dump and the key together** (the key sat next to it, or the whole server leaked). Every
   IBAN is affected.
3. **Somebody else's account.** The IBANs of everyone in a group with that account are affected:
   a member sees the payment details of the other members of their groups.

## Queries

Everyone whose IBAN is stored (case 2). No filter on deletion or on having an account: a deleted
account is soft-deleted and its IBAN is still in a dump, and a member with an account may have an
IBAN of their own in a group, separate from the account's. One person can appear twice — tell them
once:

```sql
SELECT 'account' AS kind, u.id, u.display_name, u.email, u.deleted_at
FROM users u
WHERE u.iban IS NOT NULL
UNION ALL
SELECT 'member', m.id, m.display_name, u.email, m.removed_at
FROM members m
LEFT JOIN users u ON u.id = m.user_id
WHERE m.iban IS NOT NULL;
```

Whose IBANs the account `:user_id` could see (case 3). A member with an account falls back to the
account's IBAN when the group has none of its own, so both sources count:

```sql
SELECT DISTINCT m.id, m.display_name, g.name AS group_name, u.email
FROM members viewer
JOIN groups  g ON g.id = viewer.group_id
JOIN members m ON m.group_id = viewer.group_id
LEFT JOIN users u ON u.id = m.user_id
WHERE viewer.user_id = :user_id
  AND viewer.removed_at IS NULL
  AND (m.iban IS NOT NULL OR u.iban IS NOT NULL);
```

A member without an account has no e-mail address: they can only be told through whoever added
them.

## If the key leaked

There is no key rotation yet. Every stored value carries a version prefix, `v1:`, so that a new key
can write `v2:` values next to the old ones and the old ones can be re-encrypted. Until then the key
is stored apart from the database backups — otherwise case 1 is no different from case 2.
