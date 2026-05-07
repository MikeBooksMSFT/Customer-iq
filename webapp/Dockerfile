FROM python:3.12-slim

WORKDIR /app

COPY webapp/requirements.txt /app/webapp/requirements.txt
RUN pip install --no-cache-dir -r /app/webapp/requirements.txt

COPY . /app

ENV PORT=8000

CMD ["gunicorn", "--bind", "0.0.0.0:8000", "webapp.app:app"]
