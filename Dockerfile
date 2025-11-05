FROM python:3.10-slim
WORKDIR /code
RUN apt-get update && apt-get install -y ffmpeg
COPY requirements.txt requirements.txt
RUN pip install --no-cache-dir --upgrade -r requirements.txt
COPY . .
EXPOSE 5000
CMD ["python", "app.py"]