# Serves the screenshots and their viewer as a plain website, for Render.
# Render gives the port to listen on in $PORT; nginx fills it into the template below
# when the container starts.
FROM nginx:1.27-alpine

ENV PORT=10000
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY index.html desktop.json mobile.json /usr/share/nginx/html/
COPY desktop/ /usr/share/nginx/html/desktop/
COPY mobile/ /usr/share/nginx/html/mobile/

EXPOSE 10000
