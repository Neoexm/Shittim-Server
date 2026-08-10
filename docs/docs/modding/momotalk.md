---
id: momotalk
title: 'Step 16: her MomoTalk'
---

The last thing, and the one that is pure writing. MomoTalk is the chat app - she messages you, you pick a reply, she reacts, and it unspools over her whole relationship. The importer clones none of it, so an unmodified custom student has an empty conversation.

Aru has 60 messages in 36 groups. That is the scale to aim at, and it is a script rather than a database exercise.

## The table

`AcademyMessangerExcel`, 15,426 rows for everybody. The misspelling is real and is what the table is actually called; do not go looking for `Messenger`.

Every row carries a `CharacterId` and a `MessageGroupId`, and the group ids are derivable: character id times 10,000, plus a counter. Aru's run from `100000010` to `100000350`, mostly stepping by ten so there is room to slot something in later. A student at 10100 starts at `101000010`.

A group is one exchange. Two or three messages from her, sometimes a choice for you, sometimes her reply to what you picked. Rows within a group carry their own ids and are read in order.

## How a conversation is wired

Five values of `MessageCondition` do all the work:

| Condition | What the row is |
| --- | --- |
| `None` | a plain message from her |
| `FavorRankUp` | the group opens when her relationship hits `ConditionValue` |
| `AcademySchedule` | the group is tied to a schedule event |
| `Answer` | a reply you can pick |
| `Feedback` | what she says after you pick one |

Branching is `Answer` rows with different `NextGroupId` values. Two answers pointing at two groups is a fork; two answers pointing at the same group is the illusion of one, which is what most shipped conversations actually do and there is no shame in it.

`PreConditionGroupId` chains a group behind another one, so a conversation only opens once you have had the one before it. Between that and `FavorRankUp` you have everything you need to pace a whole relationship: rank gates open the chapters, precondition chains order the scenes inside them.

`FavorScheduleId` links a group to a date event - Aru's use 100002, 100003 and 100005. That is the payoff end of the arc and it is worth leaving until you have written the ordinary conversations, because it only reads well if there is something before it.

## Pacing

`FeedbackTimeMillisec` is the pause before a message lands. Aru's rows use 1000 and 1500.

This is the field that makes MomoTalk feel like a person typing rather than a wall of text appearing, and it is the one most likely to be left at zero by someone writing rows by hand. Short reaction, 1000. A message she had to think about, 1500. A punchline lands harder with the longer pause in front of it, and this is genuinely most of the comic timing available to you.

`MessageType` is `Text` or `Image`. Aru uses only text. An image row points `ImagePath` at a sticker or a photo, and it is the same asset problem as everything else in this section - a path the game has to already know about, unless you ship it through the bundle route from [step 9](own-assets.md).

## What the server does with it

Nothing you have to configure. The client decides which groups are visible from her favor rank and its own schedule state; the server hands over the rows and tracks what you have read. So MomoTalk is one of the few tiers where writing the data is genuinely the entire job.

## Writing it

Write the whole thing in a text file first, as a script, before you touch the database. Sixty messages with branches is not something you can hold in your head while also thinking about group ids, and the ordering mistakes are much cheaper to fix in a document than across sixty rows in three copies of a database.

Then walk the script and assign: a group id per exchange, a rank gate per chapter, `NextGroupId` on every answer, a precondition on anything that has to come second. Insert last.

The rank curve worth copying is the shipped one - most of the conversations early, a couple held back for the top of the relationship. A student who says everything she has by rank 3 is finished before you have played with her.

---

That is the student. Data, art, lobby, battle model, skills, voice, lines and MomoTalk - the same list every shipped student is built from. If something in it is not showing up, [when it goes wrong](troubleshooting.md) covers the usual reasons.
