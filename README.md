# Uno TP screens

Every page of Uno TP and each case on it, as full-page screenshots in desktop
(1366 wide) and mobile (390 wide) view: 54 steps, 108 pictures.

Open `index.html` in a browser to go through them step by step. Previous and
Next, or the arrow keys, move between steps; the switch at the top changes
between the desktop and the mobile picture of the same step.

The pictures were taken on 9 October 2026 from the app running locally on a
development database with test data, at commit 059fb73 of main. This branch holds only the pictures and the viewer: no app code.

## On Render

The branch deploys as a Docker web service: `Dockerfile` serves these files with
nginx on the port Render gives it, and `render.yaml` describes the service. In
Render, create a Web Service from this repository, pick the `screenshots` branch
and the Docker runtime; nothing else needs setting.

