import google.generativeai as genai
GOOGLE_AI_API_KEY = "API_Key" 
genai.configure(api_key=GOOGLE_AI_API_KEY)

print("Finding all available models...\n")
for model in genai.list_models():
  if 'generateContent' in model.supported_generation_methods:
    print(f"Model Name: {model.name}")
    print(f"Display Name: {model.display_name}")
    print("-" * 20)

print("\n...Finished.")
print("Look for a model like 'models/gemini-pro'. Copy that 'Model Name' into your app.py file.")
