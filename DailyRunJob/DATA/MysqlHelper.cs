﻿using System;
using System.Collections.Generic;
using Git_Sandbox.DailyRunJob.Contract;
using Git_Sandbox.Model;
using myfinAPI.Model;
using MySql.Data.MySqlClient;
using static myfinAPI.Model.AssetClass;

namespace Git_Sandbox.DailyRunJob.DATA
{
	public class MysqlHelper : Ioperation
	{
		string connString = "Server = localhost; Database = myfin; Uid = root; Pwd = Welcome@1; ";
		public bool AddAssetDetails(EquityBase item)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				var command = new MySqlCommand(@"INSERT INTO myfin.assetdetails ( ISIN,name, symbol) 
												VALUES ( '" + item.assetId + "','" + item.equityName + "','" + item.symbol + "');", _conn);
				int result = command.ExecuteNonQuery();

				return true;
			}

		}

		public bool UpdateLatesNAV(EquityBase eq)
		{
			if (eq.livePrice <= 0)
				return false;
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"UPDATE myfin.equitydetails SET liveprice = " + eq.livePrice + "," +
									" dtupdated ='" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "', pb= " + eq.PB + ", " +
									" marketcap=" + eq.MarketCap + ",Totalshare=" + eq.freefloat + " WHERE ISIN = '" + eq.assetId + "';", _conn);

				int result = command.ExecuteNonQuery();
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("Updated company::" + eq.equityName + ":: Price::" + eq.livePrice + ", PB::" + eq.PB + ",MarketCap::" + eq.MarketCap);
				Console.ResetColor();
			}
			return true;
		}
		public bool UpdateCompanyDetail(EquityBase eq)
		{

			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"UPDATE myfin.equitydetails SET divLink = '" + eq.divUrl + "'," +
									" description='" + eq.sourceurl + "' WHERE (ISIN = '" + eq.assetId + "');", _conn);

				int result = command.ExecuteNonQuery();
			}
			return true;
		}
		public decimal GetLatesNAV(string equityId)
		{
			decimal result = 0;

			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"SELECT liveprice FROM myfin.equitydetails" +
									" WHERE ISIN = '" + equityId + "';", _conn);

				result = Convert.ToDecimal(command.ExecuteScalar());
			}
			return result;
		}

		public void GetEquityDetails(IList<EquityBase> eq)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"select name,dtupdated,isin, divlink,assettypeid,description,liveprice, pb,marketcap from myfin.equitydetails 
						where myfin.equitydetails.description IS NOT NULL;", _conn);
				using var reader = command.ExecuteReader();
				//IList<EquityBase> eq = new List<EquityBase>();
				try
				{
					while (reader.Read())
					{
						DateTime tim = new DateTime(1000, 1, 1);
						var ss = reader["dtupdated"];
						if (ss.ToString() != "")
							tim = Convert.ToDateTime(reader["dtupdated"]);

						eq.Add(new EquityBase()
						{
							assetId = reader["isin"].ToString(),
							sourceurl = reader["description"].ToString(),
							assetType = (AssetType)((int)reader["assettypeid"]),
							divUrl = reader["divlink"].ToString(),
							lastUpdated = tim,
							equityName = reader["name"].ToString(),
							livePrice = Convert.ToDecimal(reader["liveprice"].ToString()),
							PB = string.IsNullOrEmpty(reader["pb"].ToString()) == true ? 0 : Convert.ToDecimal(reader["pb"]),
							MarketCap = string.IsNullOrEmpty(reader["marketcap"].ToString()) == true ? 0 : Convert.ToDecimal(reader["marketcap"]),
						});
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(reader["marketcap"].ToString());
					Console.WriteLine(reader["pb"]);
				}
				//return eq;
			}

		}

		bool Ioperation.ArchiveBankAccountDetails()
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"select isin, description,assettypeid from myfin.equitydetails where myfin.equitydetails.description IS NOT NULL;", _conn);
				using var reader = command.ExecuteReader();
				IList<EquityBase> eq = new List<EquityBase>();
				while (reader.Read())
				{
					eq.Add(new EquityBase()
					{
						assetId = reader["isin"].ToString(),
						sourceurl = reader["description"].ToString(),
						assetType = (AssetType)(int)reader["assettypeid"]
					});
				}
				return true;
			}
		}

		public bool ReplaceDividendDetails(dividend item)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"REPLACE into myfin.dividend(isin,dividend,dtupdated,lastcrawleddt,typeofcredit) 
						values('" + item.companyid + "','" + item.value + "','" + item.dtUpdated.ToString("yyyy-MM-dd") + "','" +
						item.lastCrawledDate.ToString("yyyy-MM-dd HH:mm:ss") + "'," + (int)item.creditType + ");", _conn);
				int reader = command.ExecuteNonQuery();
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine(item.companyid + " Dividend Added Successfully for:: " + item.dtUpdated);
				Console.ResetColor();
				return true;
			}
		}

		public void GetEquityDetails(IList<dividend> item)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();

				using var command = new MySqlCommand(@"select * from myfin.equitydetails where description is not null and Assettypeid=1;", _conn);
				using var reader = command.ExecuteReader();

				while (reader.Read())
				{
					item.Add(new dividend()
					{
						companyid = reader["isin"].ToString(),
					});
				}
			}
		}
		public void GetTransactions(IList<EquityTransaction> p, int portfolioId)
		{

			try
			{
				using (MySqlConnection _conn = new MySqlConnection(connString))
				{
					_conn.Open();
					string query;
					//	using var command = new MySqlCommand(@"SELECT et.*,ed.liveprice,ed.assettypeid,ed.name FROM myfin.equitytransactions et
					//				Join myfin.equitydetails ed
					//				ON ed.isin=et.isin
					//				where portfolioid=" + portfolioId + ";", _conn);
					if (portfolioId != 0)
					{
						query = @"SELECT et.*,ed.liveprice,ed.assettypeid,ed.name,ed.divlink ,ed.pb eqtpb, ed.marketcap eqtmc FROM myfin.equitytransactions et
							Join myfin.equitydetails ed
							ON ed.isin=et.isin
							where portfolioid=" + portfolioId + ";";
					}
					else
					{
						query = @"SELECT et.*,ed.liveprice,ed.assettypeid,ed.name,ed.divlink,ed.pb eqtpb, ed.marketcap eqtmc FROM myfin.equitytransactions et 
							Join myfin.equitydetails ed
							ON ed.isin=et.isin;";
					}
					using MySqlCommand command = new MySqlCommand(query, _conn);
					using var reader = command.ExecuteReader();

					while (reader.Read())
					{
						//if(reader["openshare"] is DBNull)
						//{
						//	Console.WriteLine("IN");
						//}

						p.Add(new EquityTransaction()
						{
							equity = new EquityBase()
							{
								assetId = reader["isin"].ToString(),
								livePrice = Convert.ToDecimal(reader["liveprice"]),
								equityName = reader["name"].ToString(),
								divUrl = reader["divlink"].ToString(),
								MarketCap = Convert.ToDecimal(reader["eqtmc"]),
								PB = Convert.ToDecimal(reader["eqtpb"]),
								assetType = (AssetType)Convert.ToInt32(reader["assettypeid"]),

							},
							//assetTypeId= (AssetType)Convert.ToInt32(reader["assettypeid"]),
							price = Convert.ToDecimal(reader["price"]),
							portfolioId = Convert.ToInt16(reader["portfolioid"]),
							tranDate = Convert.ToDateTime(reader["TransactionDate"]),
							tranType = (TranType)Convert.ToInt16(reader["action"]),
							qty = Convert.ToInt32(reader["qty"]),
							MarketCap_Tran = Convert.ToDecimal(reader["marketcap"]),
							PB_Tran = Convert.ToDecimal(reader["pb"]),
							freefloat_tran = Convert.ToDecimal(reader["openshare"] is DBNull ? 0 : reader["openshare"]),
						});
					}
				}
			}
			catch (Exception ex)
			{
				string m = ex.Message;
			}
		}
		public void UpdateCompanyDetails(IList<EquityTransaction> res, double pb, double mv, long noShare)
		{
			try
			{
				foreach (EquityTransaction tran in res)
				{
					if (DateTime.Now.Subtract(tran.tranDate).TotalDays <= 190)
					{
						if (tran.equity.PB == 0 || tran.equity.MarketCap == 0 || tran.qty == 0)
						{
							//tran.PB = (pb / eq.LivePrice) * tran.price;
							//tran.MC = (mv / eq.LivePrice) * tran.price;
							component.getMySqlObj().UpdateTransaction(tran);
							tran.qty = noShare;
							component.getMySqlObj().UpdateTransaction(tran);
						}
					}
				}

			}
			catch (Exception ex)
			{

			}
		}
		public bool UpdateTransaction(EquityTransaction tran)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				string dt = tran.tranDate.ToString("yyyy-MM-dd");
				using var command = new MySqlCommand(@"UPDATE myfin.equitytransactions 
								SET pb= " + tran.PB_Tran + " , marketcap=" + tran.MarketCap_Tran + ", OpenShare=" + tran.equity.freefloat + " WHERE ISIN='" + tran.equity.assetId + "' AND transactiondate='" + dt + "';", _conn);
				int result = command.ExecuteNonQuery();
			}
			return true;
		}

		public void GetPropertyTransactions(IList<propertyTransaction> p, int portfolioId)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"SELECT * FROM myfin.propertytransaction 
							where portfolioid=" + portfolioId + ";", _conn);
				using var reader = command.ExecuteReader();

				while (reader.Read())
				{
					p.Add(new propertyTransaction()
					{
						astType = (myfinAPI.Model.AssetClass.AssetType)Convert.ToInt32(reader["assettype"]),
						astvalue = Convert.ToDecimal(reader["currentvalue"]),
						portfolioId = Convert.ToInt16(reader["portfolioid"]),
						TransactionDate = Convert.ToDateTime(reader["dtofpurchase"]),
						investment = Convert.ToDecimal(reader["purchaseprc"]),
						TypeofTransaction = Convert.ToChar(reader["tranmode"]),
						qty = Convert.ToDecimal(reader["qty"])
					}); ;
				}
			}
		}
		 
		public void GetCompanyDetails(EquityBase e)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				try
				{
					_conn.Open();
				}
				catch (Exception ex)
				{
					string msg = ex.Message;
				}
				using var command = new MySqlCommand(@"select * from myfin.equitydetails where isin='" + e.assetId + "';", _conn);

				using var reader = command.ExecuteReader();


				while (reader.Read())
				{
					DateTime tim = new DateTime(1000, 1, 1);
					var ss = reader["dtupdated"];
					if (ss.ToString() != "")
						tim = Convert.ToDateTime(reader["dtupdated"]);

					e.equityName = reader["Name"].ToString();
					e.livePrice = Convert.ToDecimal(reader["liveprice"]);
					e.lastUpdated = tim;
				}
			}
		}
		public bool UpdatePFSnapshot(int m, int y, int portfolioId, double invst)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"REPLACE INTO myfin.assetsnapshot(portfolioid,year,month,assettype,assetvalue,dividend,invstmt)
							 select " + portfolioId + "," + y + "," + m + ",ba.accttype ,amt,0," + invst + " from myfin.AccoutBalance bt " +
							 "join myfin.bankaccounttype ba on ba.accttypeid = bt.accttypeId " +
							 " where folioid = " + portfolioId + " and bt.accttypeid in (16, 17);", _conn);

				int result = command.ExecuteNonQuery();

				return true;

			}
		}
		public bool UpdatePFSnapshot(AssetHistory astHstry)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"REPLACE myfin.assetsnapshot
							 set  portfolioid=" + astHstry.portfolioId + ", year=" + astHstry.year + ",month=" + astHstry.month + ",assettype=" + (int)astHstry.Assettype + ",assetvalue=" + astHstry.AssetValue + ",dividend=" + astHstry.Dividend +
							 ",invstmt=" + astHstry.Investment + ";", _conn);

				int result = command.ExecuteNonQuery();

				return true;

			}
		}
		public bool UpdateBankSnapshot(int m, int y, int portfolioId)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"REPLACE INTO myfin.assetsnapshot(portfolioid,year,month,assettype,assetvalue,dividend,invstmt)
							 select " + portfolioId + "," + y + "," + m + ",6, sum(amt),0,0 from myfin.AccoutBalance bt join myfin.bankaccounttype ba " +
							 "on ba.accttypeid = bt.accttypeId" +
							 " where folioid=" + portfolioId + " and ba.accttype=6  group by folioid;", _conn);

				int result = command.ExecuteNonQuery();

				return true;

			}
		}
		public void GetCompaniesDividendDetails(IList<dividend> d, int portfolioId, int month, int year)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@" SELECT * FROM myfin.dividend 
					Where isin in (SELECT isin FROM myfin.equitytransactions where portfolioid=" + portfolioId + ") and year(dtupdated)=" + year + " and month(dtupdated)=" + month + "" +
					"	order by isin,dtupdated desc;", _conn);
				using var reader = command.ExecuteReader();

				while (reader.Read())
				{
					d.Add(new dividend()
					{
						companyid = reader["isin"].ToString(),
						dtUpdated = Convert.ToDateTime(reader["dtupdated"]),
						value = Convert.ToDecimal(reader["dividend"])
					});
				}
			}
		}
		public void GetPortFolio(IList<Portfolio> p)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"SELECT * FROM myfin.portfolio;", _conn);
				using var reader = command.ExecuteReader();

				while (reader.Read())
				{
					p.Add(new Portfolio()
					{
						FolioName = reader["folioname"].ToString(),
						folioId = Convert.ToInt32(reader["portfolioid"]),
						userid = Convert.ToInt16(reader["userid"])
					});
				}
			}
		}
		public bool AddAssetSnapshot(myfinAPI.Model.AssetHistory item)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"REPLACE into myfin.assetsnapshot(portfolioid,month,year,assetvalue,dividend,invstmt,assettype) 
										values('" + item.portfolioId + "','" + item.month + "','" + item.year + "','" +
											item.AssetValue + "','" + item.Dividend + "','" + item.Investment + "','" + (int)item.Assettype + "' );", _conn);
				int reader = command.ExecuteNonQuery();

				return true;
			}
		}
		public void GetAssetSnapshot(myfinAPI.Model.AssetHistory hstry)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"Select portfolioid,month,year,assetvalue,dividend,invstmt,assettype 
										from myfin.assetsnapshot where portfolioid=" + hstry.portfolioId + " AND month=" + hstry.month +
										" AND year=" + hstry.year + " AND assettype=" + (int)hstry.Assettype + ";", _conn);
				using var reader = command.ExecuteReader();

				while (reader.Read())
				{
					hstry.AssetValue = Convert.ToDecimal(reader["assetvalue"]);
					hstry.Dividend = Convert.ToDecimal(reader["dividend"]);
					hstry.Investment = Convert.ToDecimal(reader["invstmt"]);

				}

			}

		}

		public void getCorpActionOfCompany(dividend d)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"SELECT * FROM myfin.dividend where isin='" + d.companyid + "' order by dtupdated desc limit 1;", _conn);
				using var reader = command.ExecuteReader();

				while (reader.Read())
				{
					d.dtUpdated = Convert.ToDateTime(reader["dtupdated"]);
					d.lastCrawledDate = Convert.ToDateTime(reader["lastcrawleddt"]);
				}
			}
		}
		public void getDividendDetails(IList<dividend> d)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"SELECT * FROM myfin.dividend order by dtupdated asc ;", _conn);
				using var reader = command.ExecuteReader();

				while (reader.Read())
				{
					try
					{
						d.Add(new dividend()
						{
							companyid = reader["ISIN"].ToString(),
							dtUpdated = Convert.ToDateTime(reader["dtupdated"]),
							lastCrawledDate = Convert.ToDateTime(reader["lastcrawleddt"]),
							value = Convert.ToDecimal(reader["dividend"]),
							creditType = (TranType)Enum.Parse(typeof(TranType), reader["Typeofcredit"].ToString())
						});
					}
					catch (Exception ex)
					{
						continue;
					}
				}
			}
		}

		public bool UpdateEquityMonthlyPrice(EquityBase equityItem, int month, int yr)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"REPLACE INTO myfin.equitymonthlyprice (id, month,year,price,assetType,openshare,updatedon)
				values('" + equityItem.assetId + "'," + month + "," + yr + "," +
				equityItem.livePrice + "," + (int)equityItem.assetType + "," + equityItem.freefloat + ",'" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "');", _conn);
				using var reader = command.ExecuteReader();

				return true;
			}
		}

		public decimal GetHistoricalSharePrice(string id, int month, int year)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"SELECT * FROM myfin.equitymonthlyprice where id='" + id + "' AND year=" + year + " AND month=" +
					month + ";", _conn);
				using var reader = command.ExecuteReader();

				while (reader.Read())
				{
					return Convert.ToDecimal(reader["price"]);

				}
				return 0;
			}
		}

		public void GetPf_PPFTransaction(int folioId, IList<PFAccount> pftran, AssetType type)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"select * from myfin.pf where folioid=" + folioId + " AND acttype=" + Convert.ToInt32(type) + " order by dtofchange asc;", _conn);
				using var reader = command.ExecuteReader();

				while (reader.Read())
				{
					pftran.Add(new PFAccount()
					{
						DateOfTransaction = Convert.ToDateTime(reader["dtofchange"]),
						InvestmentEmp = Convert.ToDecimal(reader["emp"]),
						InvestmentEmplr = Convert.ToDecimal(reader["employer"]),
						Pension = Convert.ToDecimal(reader["pension"]),
						TypeOfTransaction = Enum.Parse<TranType>(reader["typeofcredit"].ToString())
					});
				}
			}
		}
		public void GetPf_PPFTransaction(int folioId, IList<PFAccount> pftran, AssetType type, int year)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"select * from myfin.pf where folioid=" + folioId + " " +
					"AND acttype=" + Convert.ToInt32(type) + " AND YEAR(dtofchange)="+ year +"  order by dtofchange asc;", _conn);
				using var reader = command.ExecuteReader();

				while (reader.Read())
				{
					pftran.Add(new PFAccount()
					{
						DateOfTransaction = Convert.ToDateTime(reader["dtofchange"]),
						InvestmentEmp = Convert.ToDecimal(reader["emp"]),
						InvestmentEmplr = Convert.ToDecimal(reader["employer"]),
						Pension = Convert.ToDecimal(reader["pension"]),
						TypeOfTransaction = Enum.Parse<TranType>(reader["typeofcredit"].ToString())
					});
				}
			}
		}

		public void GetCompaniesMissingInformation(IList<EquityBase> itemlist)
		{
			using (MySqlConnection _conn = new MySqlConnection(connString))
			{
				_conn.Open();
				using var command = new MySqlCommand(@"select name,isin from myfin.equitydetails where (description is null )AND assettypeid=1;", _conn);
				//using var command = new MySqlCommand(@"select name,isin from myfin.equitydetails where (divlink is null OR description is null )AND assettypeid=1;", _conn);

				using var reader = command.ExecuteReader();

				while (reader.Read())
				{
					itemlist.Add(
						new EquityBase()
						{
							equityName = reader["name"].ToString(),
							assetId = reader["ISIN"].ToString()
						});
				}
			}
		}
	}
}