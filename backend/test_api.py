import requests
import random

url = "http://localhost:5213/chat"
payload = {"message": f"What is AIOS? {random.randint(1,100000)}"}
try:
    response = requests.post(url, json=payload, timeout=60)
    print("STATUS:", response.status_code)
    print("RESPONSE JSON:", response.json())
except Exception as e:
    print("Error:", e)
