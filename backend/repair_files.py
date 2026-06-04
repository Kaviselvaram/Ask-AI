import os
import pyodbc
from dotenv import load_dotenv

load_dotenv()
conn_str = os.getenv("SQL_CONNECTION_STRING")
# Strip out "Server=tcp:..." format to something pyodbc uses if needed, 
# but actually C# repair script is better since it uses the exact C# SQL driver and connections string natively.
