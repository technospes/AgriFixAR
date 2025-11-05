import os
from flask import Flask, jsonify, request
from werkzeug.utils import secure_filename
import json
from ultralytics import YOLO
import whisper
import cv2
import google.generativeai as genai
from dotenv import load_dotenv

load_dotenv()

GOOGLE_AI_API_KEY = os.environ.get("GOOGLE_AI_API_KEY")
if not GOOGLE_AI_API_KEY:
    print("ERROR: GOOGLE_AI_API_KEY not found. Did you set it in .env or Hugging Face Secrets?")
genai.configure(api_key=GOOGLE_AI_API_KEY)
generation_model = genai.GenerativeModel('models/gemini-2.5-pro')
print("Generative AI model loaded.")

print("Loading local AI models...")
yolo_model = YOLO("yolov8n.pt") 
whisper_model = whisper.load_model("base")
print("Local models loaded. Server is ready.")

def run_cv_model(video_path):
    print(f"CV: Running REAL YOLOv8 model on {video_path}...")
    MODEL_CLASS_MAP = {
        "truck": "tractor",
        "bus": "tractor",
        "motorcycle": "motor",
        "person": "motor",
        "train": "thresher",
        "combine": "thresher",
        "machine": "thresher",
    }
    try:
        cap = cv2.VideoCapture(video_path)
        detected_objects = {}
        for _ in range(50):
            ret, frame = cap.read()
            if not ret: break
            results = yolo_model(frame, verbose=False)
            for r in results:
                for box in r.boxes:
                    class_name = yolo_model.names[int(box.cls[0])]
                    detected_objects[class_name] = detected_objects.get(class_name, 0) + 1
        cap.release()
        print(f"YOLO detected: {detected_objects}")
        if not detected_objects: return "unknown_machine"
        
        best_match = None
        highest_count = 0
        for obj_name, count in detected_objects.items():
            if obj_name in MODEL_CLASS_MAP and count > highest_count:
                highest_count = count
                best_match = obj_name
        
        if best_match:
            machine_id = MODEL_CLASS_MAP[best_match]
            print(f"CV Mapped '{best_match}' (count: {highest_count}) to '{machine_id}'")
            return machine_id
        else:
            print("CV found objects, but none are in our MODEL_CLASS_MAP.")
            return "unknown_machine"
    except Exception as e:
        print(f"YOLO error: {e}")
        return "unknown_machine"

def run_whisper_model(audio_path):
    print(f"VOICE: Running REAL Whisper model on {audio_path}...")
    try:
        result = whisper_model.transcribe(audio_path, fp16=False)
        transcription = result["text"]
        print(f"Whisper transcription: {transcription}")
        return transcription
    except Exception as e:
        print(f"Whisper error: {e}")
        return ""
    
def run_generative_ai_diagnosis(machine_id, problem_text):
    """
    [REAL GENERATIVE AI - RAG v3]
    This prompt FORCES the AI to obey the fact sheet.
    """
    print(f"AI BRAIN: Generating fix for '{machine_id}' with problem '{problem_text}'")
    if "tractor" in machine_id:
        manual_file = "knowledge_base/tractor_facts.txt"
    elif "motor" in machine_id:
        manual_file = "knowledge_base/motor_facts.txt"
    elif "thresher" in machine_id:
        manual_file = "knowledge_base/thresher_facts.txt"
    else:
        manual_file = None

    knowledge = ""
    if manual_file and os.path.exists(manual_file):
        with open(manual_file, 'r', encoding='utf-8') as f:
            knowledge = f.read()
        print(f"AI BRAIN: Loaded {manual_file}")
    else:
        print(f"AI BRAIN: No fact sheet found for {machine_id}.")
        knowledge = "No manual available. Use general mechanic knowledge."

    prompt = f"""
    You are an expert farm mechanic acting as a teacher for a beginner.
    Your task is to create a simple, step-by-step repair guide.

    **YOUR SOURCE OF TRUTH (Fact Sheet):**
    "{knowledge}"

    **THE FARMER'S PROBLEM (Audio):**
    "{problem_text}"

    **INSTRUCTIONS (ABSOLUTE RULES):**
    1.  **YOU MUST USE THE "SOURCE OF TRUTH" ABOVE.** Your Fact Sheet is the only correct source of information.
    2.  The farmer might use the wrong words. If the farmer says "clutch is stuck" but your Fact Sheet mentions "clutch free play" or "linkage," you MUST generate a solution for the **linkage adjustment**.
    3.  Do not use your "general knowledge" if it contradicts the Fact Sheet. The Fact Sheet is always right.
    4.  Synthesize the facts into a very simple, step-by-step guide for a total beginner.
    5.  **You MUST format your answer as a valid JSON object.**
    6.  The JSON must have "problem" (string) and "steps" (list of objects).
    7.  Each step object must have "text" (string) and "ar_model" (string) (e.g., "arrow.obj").

    **YOUR RESPONSE (JSON ONLY):**
    """
    
    try:
        generation_model = genai.GenerativeModel('models/gemini-2.5-pro') 
        
        print("AI BRAIN: Sending v3 prompt to Generative AI...")
        response = generation_model.generate_content(prompt)
        json_text = response.text
        json_text = json_text.strip().lstrip("```json").rstrip("```").strip()
        
        print(f"AI Response (JSON): {json_text}")
        
        return json.loads(json_text)
        
    except Exception as e:
        print(f"Error during AI generation: {e}")
        return {
            "problem": "AI Generation Error",
            "steps": [
                {"text": "The AI brain is having trouble. Please try again.", "ar_model": "error.obj"},
                {"text": f"Error: {e}", "ar_model": "error.obj"}
            ]
        }
UPLOAD_FOLDER = 'uploads'
if not os.path.exists(UPLOAD_FOLDER):
    os.makedirs(UPLOAD_FOLDER)

app = Flask(__name__)
app.config['UPLOAD_FOLDER'] = UPLOAD_FOLDER

@app.route('/diagnose', methods=['POST'])
def diagnose_machine():
    print("Request received!")
    if 'video' not in request.files or 'audio' not in request.files:
        return jsonify({"error": "Missing video or audio file"}), 400

    video_file = request.files['video']
    audio_file = request.files['audio']
    video_filename = secure_filename(video_file.filename)
    audio_filename = secure_filename(audio_file.filename)
    video_path = os.path.join(app.config['UPLOAD_FOLDER'], video_filename)
    audio_path = os.path.join(app.config['UPLOAD_FOLDER'], audio_filename)
    video_file.save(video_path)
    audio_file.save(audio_path)
    print(f"Files saved to {video_path} and {audio_path}")
    machine_id = run_cv_model(video_path)
    problem_text = run_whisper_model(audio_path)
    fix_json = run_generative_ai_diagnosis(machine_id, problem_text)
    return jsonify(fix_json)

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000)