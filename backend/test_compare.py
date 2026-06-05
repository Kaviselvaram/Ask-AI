import requests

url = "http://localhost:5213/chat"
payload = {"message": "Compare AIOS_Revised_Product_Blueprint.pdf and Bookreview report.docx"}
try:
    response = requests.post(url, json=payload, timeout=60)
    print("STATUS:", response.status_code)
    print("RESPONSE JSON:", response.json())
except Exception as e:
    print("Error:", e)
