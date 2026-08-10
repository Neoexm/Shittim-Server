---
id: notices
title: Notices
---

Two unrelated things that both come out of the login response: the notification bits the client reads on every reply, and the gate that can stop it getting in at all.

## Notification flags

Each flag is one bit the client checks. Setting one makes the client do something on its next reply:

| Flag | Effect |
| --- | --- |
| NewMailArrived | pops the new-mail toast in the lobby |
| HasUnreadMail | lights the mailbox badge - the server raises this by itself whenever the account has unread mail |
| NewToastDetected | client fetches and shows its queued toast notifications |
| CanReceiveArenaDailyReward | badges the arena daily reward as claimable |
| CanReceiveRaidReward | badges Total Assault rewards |
| CanReceiveEliminateRaidReward | badges Grand Assault rewards |
| CanReceiveMultiFloorRaidReward | badges Final Restriction rewards |
| CanReceiveClanAttendanceReward | badges the club attendance reward |
| CanReceiveProductDailyRecordReward | badges the monthly pass daily reward |
| ServerMaintenance | read on every reply, but the maintenance screen comes from the gate below, not from this bit |
| CannotReceiveMail | mailbox is full, so the client stops offering to claim |
| InventoryFullRewardMail | tells the player rewards went to mail because the inventory was full |
| HasClanApplicant | the club has pending join requests |
| HasFriendRequest | friend requests are waiting |
| CheckConquest | conquest state changed, refetch it |
| HasUnreadSemiPermanentMail | unread mail in the long-retention box |

## The gate

The gate makes the server answer logins with an error code instead of letting the client in. The codes the client has a dedicated screen for:

| Code | Name | Screen |
| --- | --- | --- |
| 28001 | ServerIsUnderMaintenance | full maintenance screen, nobody gets past the title |
| 28002 | ServerMaintenanceSoon | shutting-down-shortly warning |
| 28003 | AccountIsNotInWhiteList | closed-beta gate |
| 28005 | ServerContentsLock | content is locked |

Anything else lands on the client's generic popup, which is what the "other error code" field is for.

Turning the gate on locks you out too, so remember it is on before wondering why the game will not start.
