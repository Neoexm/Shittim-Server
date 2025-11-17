import sqlite3
import tkinter as tk
from tkinter import ttk, messagebox, scrolledtext
from datetime import datetime, timedelta
import json
import os

class ShittimAdminGUI:
    def __init__(self, root):
        self.root = root
        self.root.title("Shittim Server Admin Panel")
        self.root.geometry("1200x800")
        
        self.db_path = os.path.join(os.path.dirname(__file__), "Shittim-Server", "BlueArchive.db")
        
        if not os.path.exists(self.db_path):
            messagebox.showerror("Error", f"Database not found at {self.db_path}")
            self.root.destroy()
            return
        
        self.notebook = ttk.Notebook(root)
        self.notebook.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        self.create_account_tab()
        self.create_gacha_tab()
        self.create_mail_tab()
        self.create_items_tab()
        self.create_characters_tab()
        
        self.load_accounts()
    
    def get_db_connection(self):
        return sqlite3.connect(self.db_path)
    
    def create_account_tab(self):
        self.account_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.account_frame, text="Account Editor")
        
        top_frame = ttk.Frame(self.account_frame)
        top_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Label(top_frame, text="Select Account:").pack(side=tk.LEFT, padx=5)
        self.account_combo = ttk.Combobox(top_frame, width=40, state="readonly")
        self.account_combo.pack(side=tk.LEFT, padx=5)
        self.account_combo.bind("<<ComboboxSelected>>", self.load_account_data)
        
        ttk.Button(top_frame, text="Refresh", command=self.load_accounts).pack(side=tk.LEFT, padx=5)
        
        info_frame = ttk.LabelFrame(self.account_frame, text="Account Information")
        info_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        left_col = ttk.Frame(info_frame)
        left_col.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        ttk.Label(left_col, text="Nickname:").grid(row=0, column=0, sticky=tk.W, pady=5)
        self.nickname_entry = ttk.Entry(left_col, width=30)
        self.nickname_entry.grid(row=0, column=1, pady=5)
        
        ttk.Label(left_col, text="Level:").grid(row=1, column=0, sticky=tk.W, pady=5)
        self.level_entry = ttk.Entry(left_col, width=30)
        self.level_entry.grid(row=1, column=1, pady=5)
        
        ttk.Label(left_col, text="Experience:").grid(row=2, column=0, sticky=tk.W, pady=5)
        self.exp_entry = ttk.Entry(left_col, width=30)
        self.exp_entry.grid(row=2, column=1, pady=5)
        
        ttk.Label(left_col, text="Comment:").grid(row=3, column=0, sticky=tk.W, pady=5)
        self.comment_entry = ttk.Entry(left_col, width=30)
        self.comment_entry.grid(row=3, column=1, pady=5)
        
        right_col = ttk.Frame(info_frame)
        right_col.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        ttk.Label(right_col, text="Free Gems:").grid(row=0, column=0, sticky=tk.W, pady=5)
        self.gem_free_entry = ttk.Entry(right_col, width=30)
        self.gem_free_entry.grid(row=0, column=1, pady=5)
        
        ttk.Label(right_col, text="Paid Gems:").grid(row=1, column=0, sticky=tk.W, pady=5)
        self.gem_paid_entry = ttk.Entry(right_col, width=30)
        self.gem_paid_entry.grid(row=1, column=1, pady=5)
        
        ttk.Label(right_col, text="Gold:").grid(row=2, column=0, sticky=tk.W, pady=5)
        self.gold_entry = ttk.Entry(right_col, width=30)
        self.gold_entry.grid(row=2, column=1, pady=5)
        
        ttk.Label(right_col, text="AP:").grid(row=3, column=0, sticky=tk.W, pady=5)
        self.ap_entry = ttk.Entry(right_col, width=30)
        self.ap_entry.grid(row=3, column=1, pady=5)
        
        ttk.Label(right_col, text="Arena Ticket:").grid(row=4, column=0, sticky=tk.W, pady=5)
        self.arena_entry = ttk.Entry(right_col, width=30)
        self.arena_entry.grid(row=4, column=1, pady=5)
        
        ttk.Label(right_col, text="Raid Ticket:").grid(row=5, column=0, sticky=tk.W, pady=5)
        self.raid_entry = ttk.Entry(right_col, width=30)
        self.raid_entry.grid(row=5, column=1, pady=5)
        
        button_frame = ttk.Frame(self.account_frame)
        button_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Button(button_frame, text="Save Changes", command=self.save_account_data).pack(side=tk.LEFT, padx=5)
        ttk.Button(button_frame, text="Max Everything", command=self.max_account).pack(side=tk.LEFT, padx=5)
    
    def create_gacha_tab(self):
        self.gacha_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.gacha_frame, text="Gacha Manager")
        
        top_frame = ttk.Frame(self.gacha_frame)
        top_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Label(top_frame, text="Custom Gacha Rates (for testing)").pack()
        
        rates_frame = ttk.LabelFrame(self.gacha_frame, text="Gacha Rates Override")
        rates_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Label(rates_frame, text="SSR Rate (%):").grid(row=0, column=0, sticky=tk.W, padx=5, pady=5)
        self.ssr_rate_entry = ttk.Entry(rates_frame, width=20)
        self.ssr_rate_entry.grid(row=0, column=1, padx=5, pady=5)
        self.ssr_rate_entry.insert(0, "3.0")
        
        ttk.Label(rates_frame, text="SR Rate (%):").grid(row=1, column=0, sticky=tk.W, padx=5, pady=5)
        self.sr_rate_entry = ttk.Entry(rates_frame, width=20)
        self.sr_rate_entry.grid(row=1, column=1, padx=5, pady=5)
        self.sr_rate_entry.insert(0, "18.5")
        
        ttk.Label(rates_frame, text="R Rate (%):").grid(row=2, column=0, sticky=tk.W, padx=5, pady=5)
        self.r_rate_entry = ttk.Entry(rates_frame, width=20)
        self.r_rate_entry.grid(row=2, column=1, padx=5, pady=5)
        self.r_rate_entry.insert(0, "78.5")
        
        ttk.Button(rates_frame, text="Apply Custom Rates", command=self.apply_custom_rates).grid(row=3, column=0, columnspan=2, pady=10)
        ttk.Button(rates_frame, text="Reset to Default Rates", command=self.reset_rates).grid(row=4, column=0, columnspan=2, pady=5)
        
        guarantee_frame = ttk.LabelFrame(self.gacha_frame, text="Guaranteed Character")
        guarantee_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Label(guarantee_frame, text="Character ID:").grid(row=0, column=0, sticky=tk.W, padx=5, pady=5)
        self.guarantee_char_entry = ttk.Entry(guarantee_frame, width=20)
        self.guarantee_char_entry.grid(row=0, column=1, padx=5, pady=5)
        
        ttk.Button(guarantee_frame, text="Browse Characters", command=self.browse_characters).grid(row=0, column=2, padx=5)
        
        ttk.Button(guarantee_frame, text="Set Guaranteed Character", command=self.set_guaranteed_char).grid(row=1, column=0, columnspan=3, pady=10)
        ttk.Button(guarantee_frame, text="Clear Guarantee", command=self.clear_guaranteed_char).grid(row=2, column=0, columnspan=3, pady=5)
        
        banner_frame = ttk.LabelFrame(self.gacha_frame, text="Banner Management")
        banner_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        ttk.Label(banner_frame, text="Available Banners:").pack(padx=5, pady=5)
        
        banner_list_frame = ttk.Frame(banner_frame)
        banner_list_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        scrollbar = ttk.Scrollbar(banner_list_frame)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        self.banner_listbox = tk.Listbox(banner_list_frame, yscrollcommand=scrollbar.set, height=10)
        self.banner_listbox.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.config(command=self.banner_listbox.yview)
        
        banner_btn_frame = ttk.Frame(banner_frame)
        banner_btn_frame.pack(fill=tk.X, padx=5, pady=5)
        
        ttk.Button(banner_btn_frame, text="Refresh Banners", command=self.refresh_banners).pack(side=tk.LEFT, padx=5)
        ttk.Button(banner_btn_frame, text="Enable Selected", command=self.enable_banner).pack(side=tk.LEFT, padx=5)
        ttk.Button(banner_btn_frame, text="Disable Selected", command=self.disable_banner).pack(side=tk.LEFT, padx=5)
        
        self.refresh_banners()
    
    def create_mail_tab(self):
        self.mail_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.mail_frame, text="Mail Sender")
        
        top_frame = ttk.Frame(self.mail_frame)
        top_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Label(top_frame, text="Select Account:").pack(side=tk.LEFT, padx=5)
        self.mail_account_combo = ttk.Combobox(top_frame, width=40, state="readonly")
        self.mail_account_combo.pack(side=tk.LEFT, padx=5)
        
        mail_info_frame = ttk.LabelFrame(self.mail_frame, text="Mail Details")
        mail_info_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        ttk.Label(mail_info_frame, text="Sender Name:").grid(row=0, column=0, sticky=tk.W, padx=5, pady=5)
        self.mail_sender_entry = ttk.Entry(mail_info_frame, width=50)
        self.mail_sender_entry.grid(row=0, column=1, padx=5, pady=5)
        self.mail_sender_entry.insert(0, "System")
        
        ttk.Label(mail_info_frame, text="Subject:").grid(row=1, column=0, sticky=tk.W, padx=5, pady=5)
        self.mail_subject_entry = ttk.Entry(mail_info_frame, width=50)
        self.mail_subject_entry.grid(row=1, column=1, padx=5, pady=5)
        
        ttk.Label(mail_info_frame, text="Message:").grid(row=2, column=0, sticky=tk.NW, padx=5, pady=5)
        self.mail_message_text = scrolledtext.ScrolledText(mail_info_frame, width=50, height=10)
        self.mail_message_text.grid(row=2, column=1, padx=5, pady=5)
        
        ttk.Label(mail_info_frame, text="Expire Days:").grid(row=3, column=0, sticky=tk.W, padx=5, pady=5)
        self.mail_expire_entry = ttk.Entry(mail_info_frame, width=50)
        self.mail_expire_entry.grid(row=3, column=1, padx=5, pady=5)
        self.mail_expire_entry.insert(0, "30")
        
        items_frame = ttk.LabelFrame(self.mail_frame, text="Mail Rewards")
        items_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Label(items_frame, text="Item Type:").grid(row=0, column=0, sticky=tk.W, padx=5, pady=5)
        self.mail_item_type = ttk.Combobox(items_frame, values=["Currency", "Item", "Character", "Equipment"], state="readonly", width=20)
        self.mail_item_type.grid(row=0, column=1, padx=5, pady=5)
        self.mail_item_type.set("Currency")
        
        ttk.Label(items_frame, text="Item ID:").grid(row=1, column=0, sticky=tk.W, padx=5, pady=5)
        self.mail_item_id_entry = ttk.Entry(items_frame, width=20)
        self.mail_item_id_entry.grid(row=1, column=1, padx=5, pady=5)
        
        ttk.Label(items_frame, text="Amount:").grid(row=2, column=0, sticky=tk.W, padx=5, pady=5)
        self.mail_item_amount_entry = ttk.Entry(items_frame, width=20)
        self.mail_item_amount_entry.grid(row=2, column=1, padx=5, pady=5)
        self.mail_item_amount_entry.insert(0, "1")
        
        ttk.Button(items_frame, text="Add Item", command=self.add_mail_item).grid(row=3, column=0, columnspan=2, pady=10)
        
        self.mail_items_list = tk.Listbox(items_frame, height=5)
        self.mail_items_list.grid(row=4, column=0, columnspan=2, padx=5, pady=5, sticky=tk.EW)
        
        ttk.Button(items_frame, text="Remove Selected Item", command=self.remove_mail_item).grid(row=5, column=0, columnspan=2, pady=5)
        
        self.mail_items = []
        
        button_frame = ttk.Frame(self.mail_frame)
        button_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Button(button_frame, text="Send Mail", command=self.send_mail).pack(side=tk.LEFT, padx=5)
        ttk.Button(button_frame, text="Clear Form", command=self.clear_mail_form).pack(side=tk.LEFT, padx=5)
    
    def create_items_tab(self):
        self.items_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.items_frame, text="Item Spawner")
        
        top_frame = ttk.Frame(self.items_frame)
        top_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Label(top_frame, text="Select Account:").pack(side=tk.LEFT, padx=5)
        self.item_account_combo = ttk.Combobox(top_frame, width=40, state="readonly")
        self.item_account_combo.pack(side=tk.LEFT, padx=5)
        
        spawn_frame = ttk.LabelFrame(self.items_frame, text="Spawn Item")
        spawn_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        ttk.Label(spawn_frame, text="Item ID:").grid(row=0, column=0, sticky=tk.W, padx=5, pady=5)
        self.spawn_item_id_entry = ttk.Entry(spawn_frame, width=30)
        self.spawn_item_id_entry.grid(row=0, column=1, padx=5, pady=5)
        
        ttk.Label(spawn_frame, text="Amount:").grid(row=1, column=0, sticky=tk.W, padx=5, pady=5)
        self.spawn_item_amount_entry = ttk.Entry(spawn_frame, width=30)
        self.spawn_item_amount_entry.grid(row=1, column=1, padx=5, pady=5)
        self.spawn_item_amount_entry.insert(0, "1")
        
        ttk.Label(spawn_frame, text="Stack Count:").grid(row=2, column=0, sticky=tk.W, padx=5, pady=5)
        self.spawn_item_stack_entry = ttk.Entry(spawn_frame, width=30)
        self.spawn_item_stack_entry.grid(row=2, column=1, padx=5, pady=5)
        self.spawn_item_stack_entry.insert(0, "1")
        
        ttk.Button(spawn_frame, text="Spawn Item", command=self.spawn_item).grid(row=3, column=0, columnspan=2, pady=10)
        
        list_frame = ttk.LabelFrame(self.items_frame, text="Current Items")
        list_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        self.items_listbox = tk.Listbox(list_frame, height=15)
        self.items_listbox.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        ttk.Button(list_frame, text="Refresh Items", command=self.refresh_items).pack(pady=5)
    
    def create_characters_tab(self):
        self.characters_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.characters_frame, text="Character Spawner")
        
        top_frame = ttk.Frame(self.characters_frame)
        top_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Label(top_frame, text="Select Account:").pack(side=tk.LEFT, padx=5)
        self.char_account_combo = ttk.Combobox(top_frame, width=40, state="readonly")
        self.char_account_combo.pack(side=tk.LEFT, padx=5)
        
        spawn_frame = ttk.LabelFrame(self.characters_frame, text="Spawn Character")
        spawn_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        ttk.Label(spawn_frame, text="Character ID:").grid(row=0, column=0, sticky=tk.W, padx=5, pady=5)
        self.spawn_char_id_entry = ttk.Entry(spawn_frame, width=30)
        self.spawn_char_id_entry.grid(row=0, column=1, padx=5, pady=5)
        
        ttk.Label(spawn_frame, text="Star Grade (1-5):").grid(row=1, column=0, sticky=tk.W, padx=5, pady=5)
        self.spawn_char_star_entry = ttk.Entry(spawn_frame, width=30)
        self.spawn_char_star_entry.grid(row=1, column=1, padx=5, pady=5)
        self.spawn_char_star_entry.insert(0, "3")
        
        ttk.Label(spawn_frame, text="Level:").grid(row=2, column=0, sticky=tk.W, padx=5, pady=5)
        self.spawn_char_level_entry = ttk.Entry(spawn_frame, width=30)
        self.spawn_char_level_entry.grid(row=2, column=1, padx=5, pady=5)
        self.spawn_char_level_entry.insert(0, "1")
        
        ttk.Button(spawn_frame, text="Spawn Character", command=self.spawn_character).grid(row=3, column=0, columnspan=2, pady=10)
        
        list_frame = ttk.LabelFrame(self.characters_frame, text="Current Characters")
        list_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        self.characters_listbox = tk.Listbox(list_frame, height=15)
        self.characters_listbox.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        ttk.Button(list_frame, text="Refresh Characters", command=self.refresh_characters).pack(pady=5)
    
    def load_accounts(self):
        try:
            conn = self.get_db_connection()
            cursor = conn.cursor()
            cursor.execute("SELECT ServerId, Nickname FROM Accounts")
            accounts = cursor.fetchall()
            conn.close()
            
            account_list = [f"{acc[0]} - {acc[1]}" for acc in accounts]
            self.account_combo['values'] = account_list
            self.mail_account_combo['values'] = account_list
            self.item_account_combo['values'] = account_list
            self.char_account_combo['values'] = account_list
            
            if account_list:
                self.account_combo.current(0)
                self.mail_account_combo.current(0)
                self.item_account_combo.current(0)
                self.char_account_combo.current(0)
                self.load_account_data()
        except Exception as e:
            messagebox.showerror("Error", f"Failed to load accounts: {str(e)}")
    
    def load_account_data(self, event=None):
        try:
            account_str = self.account_combo.get()
            if not account_str:
                return
            
            server_id = int(account_str.split(" - ")[0])
            
            conn = self.get_db_connection()
            cursor = conn.cursor()
            
            cursor.execute("SELECT Nickname, Level, Exp, Comment FROM Accounts WHERE ServerId = ?", (server_id,))
            account = cursor.fetchone()
            
            if account:
                self.nickname_entry.delete(0, tk.END)
                self.nickname_entry.insert(0, account[0])
                self.level_entry.delete(0, tk.END)
                self.level_entry.insert(0, account[1])
                self.exp_entry.delete(0, tk.END)
                self.exp_entry.insert(0, account[2])
                self.comment_entry.delete(0, tk.END)
                self.comment_entry.insert(0, account[3] or "")
            
            cursor.execute("SELECT CurrencyDict FROM Currencies WHERE AccountServerId = ?", (server_id,))
            currency_row = cursor.fetchone()
            
            if currency_row and currency_row[0]:
                currency_dict = json.loads(currency_row[0])
                
                self.gem_free_entry.delete(0, tk.END)
                self.gem_free_entry.insert(0, currency_dict.get("GemBonus", 0))
                
                self.gem_paid_entry.delete(0, tk.END)
                self.gem_paid_entry.insert(0, currency_dict.get("GemPaid", 0))
                
                self.gold_entry.delete(0, tk.END)
                self.gold_entry.insert(0, currency_dict.get("Gold", 0))
                
                self.ap_entry.delete(0, tk.END)
                self.ap_entry.insert(0, currency_dict.get("ActionPoint", 0))
                
                self.arena_entry.delete(0, tk.END)
                self.arena_entry.insert(0, currency_dict.get("ArenaTicket", 0))
                
                self.raid_entry.delete(0, tk.END)
                self.raid_entry.insert(0, currency_dict.get("RaidTicket", 0))
            else:
                self.gem_free_entry.delete(0, tk.END)
                self.gem_free_entry.insert(0, "0")
                self.gem_paid_entry.delete(0, tk.END)
                self.gem_paid_entry.insert(0, "0")
                self.gold_entry.delete(0, tk.END)
                self.gold_entry.insert(0, "0")
                self.ap_entry.delete(0, tk.END)
                self.ap_entry.insert(0, "0")
                self.arena_entry.delete(0, tk.END)
                self.arena_entry.insert(0, "0")
                self.raid_entry.delete(0, tk.END)
                self.raid_entry.insert(0, "0")
            
            conn.close()
        except Exception as e:
            messagebox.showerror("Error", f"Failed to load account data: {str(e)}")
    
    def save_account_data(self):
        try:
            account_str = self.account_combo.get()
            if not account_str:
                messagebox.showerror("Error", "No account selected")
                return
            
            server_id = int(account_str.split(" - ")[0])
            
            conn = self.get_db_connection()
            cursor = conn.cursor()
            
            cursor.execute("UPDATE Accounts SET Nickname = ?, Level = ?, Exp = ?, Comment = ? WHERE ServerId = ?",
                          (self.nickname_entry.get(), int(self.level_entry.get()), int(self.exp_entry.get()), 
                           self.comment_entry.get(), server_id))
            
            cursor.execute("SELECT CurrencyDict FROM Currencies WHERE AccountServerId = ?", (server_id,))
            currency_row = cursor.fetchone()
            
            if currency_row and currency_row[0]:
                currency_dict = json.loads(currency_row[0])
            else:
                currency_dict = {}
            
            currency_dict["GemBonus"] = int(self.gem_free_entry.get())
            currency_dict["GemPaid"] = int(self.gem_paid_entry.get())
            currency_dict["Gold"] = int(self.gold_entry.get())
            currency_dict["ActionPoint"] = int(self.ap_entry.get())
            currency_dict["ArenaTicket"] = int(self.arena_entry.get())
            currency_dict["RaidTicket"] = int(self.raid_entry.get())
            
            cursor.execute("UPDATE Currencies SET CurrencyDict = ? WHERE AccountServerId = ?",
                          (json.dumps(currency_dict), server_id))
            
            conn.commit()
            conn.close()
            
            messagebox.showinfo("Success", "Account data saved successfully")
            self.load_accounts()
        except Exception as e:
            messagebox.showerror("Error", f"Failed to save account data: {str(e)}")
    
    def max_account(self):
        try:
            self.gem_free_entry.delete(0, tk.END)
            self.gem_free_entry.insert(0, "999999999")
            
            self.gem_paid_entry.delete(0, tk.END)
            self.gem_paid_entry.insert(0, "999999999")
            
            self.gold_entry.delete(0, tk.END)
            self.gold_entry.insert(0, "999999999")
            
            self.ap_entry.delete(0, tk.END)
            self.ap_entry.insert(0, "999999")
            
            self.arena_entry.delete(0, tk.END)
            self.arena_entry.insert(0, "999999")
            
            self.raid_entry.delete(0, tk.END)
            self.raid_entry.insert(0, "999999")
            
            self.level_entry.delete(0, tk.END)
            self.level_entry.insert(0, "90")
            
            messagebox.showinfo("Success", "Set all values to max. Click 'Save Changes' to apply.")
        except Exception as e:
            messagebox.showerror("Error", f"Failed to max account: {str(e)}")
    
    def apply_custom_rates(self):
        try:
            ssr = float(self.ssr_rate_entry.get())
            sr = float(self.sr_rate_entry.get())
            r = float(self.r_rate_entry.get())
            
            if abs((ssr + sr + r) - 100.0) > 0.01:
                messagebox.showerror("Error", "Rates must add up to 100%")
                return
            
            config_path = os.path.join(os.path.dirname(__file__), "gacha_config.json")
            config = {
                "custom_rates": {
                    "ssr": ssr,
                    "sr": sr,
                    "r": r
                },
                "guaranteed_character": None
            }
            
            with open(config_path, 'w') as f:
                json.dump(config, f, indent=2)
            
            messagebox.showinfo("Success", "Custom rates saved to gacha_config.json\nNote: This requires server-side implementation to take effect")
        except Exception as e:
            messagebox.showerror("Error", f"Failed to apply custom rates: {str(e)}")
    
    def reset_rates(self):
        self.ssr_rate_entry.delete(0, tk.END)
        self.ssr_rate_entry.insert(0, "3.0")
        
        self.sr_rate_entry.delete(0, tk.END)
        self.sr_rate_entry.insert(0, "18.5")
        
        self.r_rate_entry.delete(0, tk.END)
        self.r_rate_entry.insert(0, "78.5")
        
        config_path = os.path.join(os.path.dirname(__file__), "gacha_config.json")
        if os.path.exists(config_path):
            os.remove(config_path)
        
        messagebox.showinfo("Success", "Rates reset to default")
    
    def set_guaranteed_char(self):
        try:
            char_id = int(self.guarantee_char_entry.get())
            
            config_path = os.path.join(os.path.dirname(__file__), "gacha_config.json")
            config = {"guaranteed_character": char_id, "custom_rates": None}
            
            if os.path.exists(config_path):
                with open(config_path, 'r') as f:
                    config = json.load(f)
                config["guaranteed_character"] = char_id
            
            with open(config_path, 'w') as f:
                json.dump(config, f, indent=2)
            
            messagebox.showinfo("Success", f"Guaranteed character set to ID {char_id}\nNote: This requires server-side implementation to take effect")
        except Exception as e:
            messagebox.showerror("Error", f"Failed to set guaranteed character: {str(e)}")
    
    def clear_guaranteed_char(self):
        config_path = os.path.join(os.path.dirname(__file__), "gacha_config.json")
        if os.path.exists(config_path):
            try:
                with open(config_path, 'r') as f:
                    config = json.load(f)
                config["guaranteed_character"] = None
                
                with open(config_path, 'w') as f:
                    json.dump(config, f, indent=2)
            except:
                pass
        
        self.guarantee_char_entry.delete(0, tk.END)
        messagebox.showinfo("Success", "Guaranteed character cleared")
    
    def browse_characters(self):
        browse_window = tk.Toplevel(self.root)
        browse_window.title("Character Browser")
        browse_window.geometry("600x500")
        
        search_frame = ttk.Frame(browse_window)
        search_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Label(search_frame, text="Search:").pack(side=tk.LEFT, padx=5)
        search_entry = ttk.Entry(search_frame, width=30)
        search_entry.pack(side=tk.LEFT, padx=5)
        
        search_button = ttk.Button(search_frame, text="Search")
        search_button.pack(side=tk.LEFT, padx=5)
        
        list_frame = ttk.Frame(browse_window)
        list_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        scrollbar = ttk.Scrollbar(list_frame)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        char_listbox = tk.Listbox(list_frame, yscrollcommand=scrollbar.set, height=20)
        char_listbox.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.config(command=char_listbox.yview)
        
        characters = self.get_character_list()
        
        def update_list(search_term=""):
            char_listbox.delete(0, tk.END)
            for char_id, char_name in characters:
                if search_term.lower() in char_name.lower() or search_term in str(char_id):
                    char_listbox.insert(tk.END, f"[{char_id}] {char_name}")
        
        def select_character():
            selection = char_listbox.curselection()
            if selection:
                selected_text = char_listbox.get(selection[0])
                char_id = selected_text.split("]")[0].split("[")[1]
                self.guarantee_char_entry.delete(0, tk.END)
                self.guarantee_char_entry.insert(0, char_id)
                browse_window.destroy()
        
        search_button.config(command=lambda: update_list(search_entry.get()))
        search_entry.bind("<Return>", lambda e: update_list(search_entry.get()))
        
        select_btn = ttk.Button(browse_window, text="Select Character", command=select_character)
        select_btn.pack(pady=10)
        
        update_list()
    
    def get_character_list(self):
        common_characters = [
            (10000, "Aru"), (10001, "Aru (New Year)"), (10002, "Hina"),
            (10003, "Hina (Swimsuit)"), (10004, "Iori"), (10005, "Iori (Swimsuit)"),
            (10006, "Eimi"), (10007, "Eimi (Swimsuit)"), (10008, "Shiroko"),
            (10009, "Shiroko (Riding)"), (10010, "Hoshino"), (10011, "Hoshino (Swimsuit)"),
            (10012, "Serika"), (10013, "Serika (New Year)"), (10014, "Nonomi"),
            (10015, "Nonomi (Swimsuit)"), (10016, "Ayane"), (10017, "Ayane (Swimsuit)"),
            (13000, "Yuzu"), (13001, "Yuzu (Maid)"), (16000, "Neru"), (16001, "Neru (Bunny Girl)"),
            (10018, "Akane"), (10019, "Akane (Bunny Girl)"), (10020, "Mutsuki"),
            (10021, "Mutsuki (New Year)"), (10022, "Kayoko"), (10023, "Kayoko (New Year)"),
            (10024, "Haruka"), (10025, "Haruka (New Year)"), (10026, "Atsuko"),
            (10027, "Junko"), (10028, "Izumi"), (10029, "Izumi (Swimsuit)"),
            (10030, "Shun"), (10031, "Shun (Kid)"), (10032, "Tsubaki"),
            (10033, "Tsubaki (Guide)"), (10034, "Moe"), (10035, "Midori"),
            (10036, "Momoi"), (10037, "Midori (Momotalk)"), (10038, "Yuuka"),
            (10039, "Noa"), (10040, "Karin"), (10041, "Asuna"), (10042, "Asuna (Bunny Girl)"),
            (10043, "Akari"), (10044, "Mutsuki"), (10045, "Haruna"),
            (10046, "Haruna (New Year)"), (10047, "Izuna"), (10048, "Izuna (Swimsuit)"),
            (10049, "Tsurugi"), (10050, "Tsurugi (Swimsuit)"), (10051, "Hifumi"),
            (10052, "Hifumi (Swimsuit)"), (10053, "Azusa"), (10054, "Azusa (Swimsuit)"),
            (10055, "Koharu"), (10056, "Hanae"), (10057, "Hanae (Christmas)"),
            (13002, "Aru (Dress)"), (13003, "Kayoko (Dress)"), (16002, "Asuna (Christmas)"),
            (10058, "Saya"), (10059, "Saya (Casual)"), (20001, "Serina"),
            (20002, "Shimiko"), (20003, "Ayane (Swimsuit)"), (20004, "Fuuka"),
            (20005, "Kotama"), (20006, "Juri"), (20007, "Serika (Christmas)"),
            (20008, "Toki"), (10060, "Maki"), (10061, "Cherino"), (10062, "Marina"),
        ]
        
        return common_characters
    
    def refresh_banners(self):
        try:
            self.banner_listbox.delete(0, tk.END)
            
            common_banners = [
                ("Normal Gacha", "Standard recruitment banner", True),
                ("Pickup Gacha", "Rate-up banner for featured characters", True),
                ("Limited Gacha", "Limited-time exclusive characters", True),
                ("Fest Gacha", "Festival banner with increased SSR rates", True),
            ]
            
            config_path = os.path.join(os.path.dirname(__file__), "banner_config.json")
            disabled_banners = []
            
            if os.path.exists(config_path):
                try:
                    with open(config_path, 'r') as f:
                        config = json.load(f)
                        disabled_banners = config.get("disabled_banners", [])
                except:
                    pass
            
            for banner_name, banner_desc, enabled in common_banners:
                is_enabled = banner_name not in disabled_banners
                status = "✓" if is_enabled else "✗"
                self.banner_listbox.insert(tk.END, f"{status} {banner_name} - {banner_desc}")
            
        except Exception as e:
            messagebox.showerror("Error", f"Failed to refresh banners: {str(e)}")
    
    def enable_banner(self):
        try:
            selection = self.banner_listbox.curselection()
            if not selection:
                messagebox.showerror("Error", "No banner selected")
                return
            
            selected_text = self.banner_listbox.get(selection[0])
            banner_name = selected_text.split(" - ")[0].replace("✓ ", "").replace("✗ ", "")
            
            config_path = os.path.join(os.path.dirname(__file__), "banner_config.json")
            config = {"disabled_banners": []}
            
            if os.path.exists(config_path):
                with open(config_path, 'r') as f:
                    config = json.load(f)
            
            if banner_name in config["disabled_banners"]:
                config["disabled_banners"].remove(banner_name)
            
            with open(config_path, 'w') as f:
                json.dump(config, f, indent=2)
            
            messagebox.showinfo("Success", f"Enabled {banner_name}")
            self.refresh_banners()
        except Exception as e:
            messagebox.showerror("Error", f"Failed to enable banner: {str(e)}")
    
    def disable_banner(self):
        try:
            selection = self.banner_listbox.curselection()
            if not selection:
                messagebox.showerror("Error", "No banner selected")
                return
            
            selected_text = self.banner_listbox.get(selection[0])
            banner_name = selected_text.split(" - ")[0].replace("✓ ", "").replace("✗ ", "")
            
            config_path = os.path.join(os.path.dirname(__file__), "banner_config.json")
            config = {"disabled_banners": []}
            
            if os.path.exists(config_path):
                with open(config_path, 'r') as f:
                    config = json.load(f)
            
            if banner_name not in config["disabled_banners"]:
                config["disabled_banners"].append(banner_name)
            
            with open(config_path, 'w') as f:
                json.dump(config, f, indent=2)
            
            messagebox.showinfo("Success", f"Disabled {banner_name}")
            self.refresh_banners()
        except Exception as e:
            messagebox.showerror("Error", f"Failed to disable banner: {str(e)}")

    
    def add_mail_item(self):
        try:
            item_type = self.mail_item_type.get()
            item_id = int(self.mail_item_id_entry.get())
            amount = int(self.mail_item_amount_entry.get())
            
            self.mail_items.append({
                "type": item_type,
                "id": item_id,
                "amount": amount
            })
            
            self.mail_items_list.insert(tk.END, f"{item_type} - ID: {item_id} x {amount}")
            
            self.mail_item_id_entry.delete(0, tk.END)
            self.mail_item_amount_entry.delete(0, tk.END)
            self.mail_item_amount_entry.insert(0, "1")
        except Exception as e:
            messagebox.showerror("Error", f"Failed to add item: {str(e)}")
    
    def remove_mail_item(self):
        try:
            selection = self.mail_items_list.curselection()
            if selection:
                index = selection[0]
                self.mail_items_list.delete(index)
                self.mail_items.pop(index)
        except Exception as e:
            messagebox.showerror("Error", f"Failed to remove item: {str(e)}")
    
    def send_mail(self):
        try:
            account_str = self.mail_account_combo.get()
            if not account_str:
                messagebox.showerror("Error", "No account selected")
                return
            
            server_id = int(account_str.split(" - ")[0])
            
            sender = self.mail_sender_entry.get()
            subject = self.mail_subject_entry.get()
            message = self.mail_message_text.get("1.0", tk.END).strip()
            expire_days = int(self.mail_expire_entry.get())
            
            if not subject:
                messagebox.showerror("Error", "Subject is required")
                return
            
            conn = self.get_db_connection()
            cursor = conn.cursor()
            
            send_date = datetime.utcnow()
            if expire_days == 0:
                expire_date = send_date.replace(year=send_date.year + 1)
            else:
                expire_date = send_date + timedelta(days=expire_days)
            
            mail_type = 1
            
            parcel_json = json.dumps([{
                "Key": {"Type": self.get_parcel_type(item["type"]), "Id": item["id"]},
                "Amount": item["amount"]
            } for item in self.mail_items])
            
            localized_sender = json.dumps({
                "0": sender, "1": sender, "2": sender, "3": sender, "4": sender
            })
            localized_comment = json.dumps({
                "0": message, "1": message, "2": message, "3": message, "4": message
            })
            
            cursor.execute("""
                INSERT INTO Mails (AccountServerId, Type, UniqueId, Sender, Comment, LocalizedSender, LocalizedComment, SendDate, ExpireDate, ParcelInfos, RemainParcelInfos, IsRefresher)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (server_id, mail_type, 0, sender, message, localized_sender, localized_comment, 
                  send_date.strftime("%Y-%m-%d %H:%M:%S"), expire_date.strftime("%Y-%m-%d %H:%M:%S"), 
                  parcel_json, "[]", 0))
            
            conn.commit()
            conn.close()
            
            messagebox.showinfo("Success", f"Mail sent successfully to account {server_id}")
            self.clear_mail_form()
        except Exception as e:
            messagebox.showerror("Error", f"Failed to send mail: {str(e)}")
    
    def get_parcel_type(self, item_type):
        mapping = {
            "Currency": 1,
            "Item": 2,
            "Character": 4,
            "Equipment": 3
        }
        return mapping.get(item_type, 2)
    
    def clear_mail_form(self):
        self.mail_subject_entry.delete(0, tk.END)
        self.mail_message_text.delete("1.0", tk.END)
        self.mail_item_id_entry.delete(0, tk.END)
        self.mail_item_amount_entry.delete(0, tk.END)
        self.mail_item_amount_entry.insert(0, "1")
        self.mail_items_list.delete(0, tk.END)
        self.mail_items = []
    
    def spawn_item(self):
        try:
            account_str = self.item_account_combo.get()
            if not account_str:
                messagebox.showerror("Error", "No account selected")
                return
            
            server_id = int(account_str.split(" - ")[0])
            item_id = int(self.spawn_item_id_entry.get())
            amount = int(self.spawn_item_amount_entry.get())
            stack_count = int(self.spawn_item_stack_entry.get())
            
            conn = self.get_db_connection()
            cursor = conn.cursor()
            
            cursor.execute("SELECT ServerId FROM Items WHERE ServerId = ? AND UniqueId = ?", (server_id, item_id))
            existing = cursor.fetchone()
            
            if existing:
                cursor.execute("UPDATE Items SET StackCount = StackCount + ? WHERE ServerId = ? AND UniqueId = ?",
                              (amount, server_id, item_id))
            else:
                cursor.execute("""
                    INSERT INTO Items (ServerId, UniqueId, StackCount, IsNew, IsLocked)
                    VALUES (?, ?, ?, ?, ?)
                """, (server_id, item_id, stack_count, 1, 0))
            
            conn.commit()
            conn.close()
            
            messagebox.showinfo("Success", f"Item {item_id} spawned successfully")
            self.refresh_items()
        except Exception as e:
            messagebox.showerror("Error", f"Failed to spawn item: {str(e)}")
    
    def refresh_items(self):
        try:
            account_str = self.item_account_combo.get()
            if not account_str:
                return
            
            server_id = int(account_str.split(" - ")[0])
            
            conn = self.get_db_connection()
            cursor = conn.cursor()
            cursor.execute("SELECT UniqueId, StackCount FROM Items WHERE ServerId = ?", (server_id,))
            items = cursor.fetchall()
            conn.close()
            
            self.items_listbox.delete(0, tk.END)
            for item in items:
                self.items_listbox.insert(tk.END, f"Item ID: {item[0]} - Count: {item[1]}")
        except Exception as e:
            messagebox.showerror("Error", f"Failed to refresh items: {str(e)}")
    
    def spawn_character(self):
        try:
            account_str = self.char_account_combo.get()
            if not account_str:
                messagebox.showerror("Error", "No account selected")
                return
            
            server_id = int(account_str.split(" - ")[0])
            char_id = int(self.spawn_char_id_entry.get())
            star_grade = int(self.spawn_char_star_entry.get())
            level = int(self.spawn_char_level_entry.get())
            
            conn = self.get_db_connection()
            cursor = conn.cursor()
            
            cursor.execute("SELECT MAX(ServerId) FROM Characters WHERE AccountServerId = ?", (server_id,))
            max_char_server_id = cursor.fetchone()[0]
            next_char_server_id = (max_char_server_id or 0) + 1
            
            cursor.execute("""
                INSERT INTO Characters (ServerId, AccountServerId, UniqueId, StarGrade, Level, 
                                        Exp, FavorRank, FavorExp, PublicSkillLevel, ExSkillLevel, 
                                        PassiveSkillLevel, ExtraPassiveSkillLevel, LeaderSkillLevel, 
                                        IsNew, IsLocked, EquipmentServerIds)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (next_char_server_id, server_id, char_id, star_grade, level, 0, 1, 0, 1, 1, 1, 1, 1, 1, 0, "[]"))
            
            conn.commit()
            conn.close()
            
            messagebox.showinfo("Success", f"Character {char_id} spawned successfully")
            self.refresh_characters()
        except Exception as e:
            messagebox.showerror("Error", f"Failed to spawn character: {str(e)}")
    
    def refresh_characters(self):
        try:
            account_str = self.char_account_combo.get()
            if not account_str:
                return
            
            server_id = int(account_str.split(" - ")[0])
            
            conn = self.get_db_connection()
            cursor = conn.cursor()
            cursor.execute("SELECT UniqueId, StarGrade, Level FROM Characters WHERE AccountServerId = ?", (server_id,))
            characters = cursor.fetchall()
            conn.close()
            
            self.characters_listbox.delete(0, tk.END)
            for char in characters:
                self.characters_listbox.insert(tk.END, f"Character ID: {char[0]} - {char[1]}★ Lv.{char[2]}")
        except Exception as e:
            messagebox.showerror("Error", f"Failed to refresh characters: {str(e)}")

if __name__ == "__main__":
    root = tk.Tk()
    app = ShittimAdminGUI(root)
    root.mainloop()
