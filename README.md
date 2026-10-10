# Uno TP screens

Every page of Uno TP and each case on it, as full-page screenshots in desktop
(1366 wide) and mobile (390 wide) view: 111 steps, 222 pictures. Steps 1 to 64
are Uno TP. The rest are apps of their own that read the same database: OVD
Explorer (65 to 71), DMS Explorer (72 to 83), E-Sarathi Login (84 to 102) and
E-Sarathi Console (103 to 111).

Open `index.html` in a browser to go through them step by step. Previous and
Next, or the arrow keys, move between steps; the switch at the top changes
between the desktop and the mobile picture of the same step.

The pictures were taken on 9 and 10 October 2026 from the apps running locally
on a development database with test data. Uno TP's are from commit 059fb73 of
main, except the desktop pictures of the pages with "Back to dashboard" over
the first card (2 to 5, 43 to 46 and 52 to 59), which are from 2dfea25, where
those pages start closer to the header. The other four apps are in
repositories of their own, not in this one. The OVD Explorer pictures are of
its first version: the application page has been rebuilt since. DMS Explorer
lists completed applications only (status APR); its Upload documents page is
still a prototype that stores nothing. This branch holds only the pictures and
the viewer: no app code.

## On Render

The branch deploys as a Docker web service: `Dockerfile` serves these files with
nginx on the port Render gives it, and `render.yaml` describes the service. In
Render, create a Web Service from this repository, pick the `screenshots` branch
and the Docker runtime; nothing else needs setting.

