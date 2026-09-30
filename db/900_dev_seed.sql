/* =============================================================================
   Uno TP - DEVELOPMENT AND TEST ONLY. Never run against production.

   The demo partners the web app's demo sign-in comes in as (Entry:DemoUserId in
   appsettings.Development.json), their menus, stand-in Axis CMS locations, and
   test records for the purchase journey's masters. Run after 004.
   ============================================================================= */
SET NOCOUNT ON;
GO
INSERT dbo.t_Unotp_Partner_Mst (c_User_Id, c_Name, c_Code, c_Agency_Type, c_Broker_Code, c_Branch, c_Created_By)
SELECT v.u, v.n, v.u, v.a, v.b, v.br, 'DEVSEED' FROM (VALUES
    ('100002225', N'Shivaprasad Terse', '1033', 'BR10021', N'Pune — Shivajinagar'),
    ('100002226', N'Rohan Deshmukh', '2001', 'BR10874', N'Mumbai — Andheri'),
    ('100002227', N'Kavita Rao', '2001', 'BR10877', N'Thane — Naupada')
) v (u, n, a, b, br)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_Partner_Mst p WHERE p.c_User_Id = v.u);
GO
-- Every feature for the first; no pay-in slips or console for the second; none for the third.
INSERT dbo.t_Unotp_Partner_Menu (c_User_Id, c_Feature_Key, c_Created_By)
SELECT p.u, f.c_Feature_Key, 'DEVSEED'
FROM (VALUES ('100002225'), ('100002226')) p (u)
CROSS JOIN dbo.t_Unotp_Feature_Mst f
WHERE NOT (p.u = '100002226' AND f.c_Feature_Key IN ('pis', 'admin'))
  AND NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_Partner_Menu m WHERE m.c_User_Id = p.u AND m.c_Feature_Key = f.c_Feature_Key);
GO
INSERT dbo.t_Unotp_Ref_List (c_List, n_Seq, c_Code, c_Name, c_Created_By)
SELECT 'cmsLocations', v.s, v.n, v.n, 'DEVSEED' FROM (VALUES
    (1, N'Mumbai — Andheri'), (2, N'Mumbai — Fort'), (3, N'Thane — Naupada'), (4, N'Pune — Shivajinagar')
) v (s, n)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_Ref_List r WHERE r.c_List = 'cmsLocations' AND r.c_Code = v.n);
GO

/* ----- The purchase journey's masters (db/004): the test records the demo uses.
   Every PAN starts XXXX, so nothing here can be mistaken for a real one. ------ */
INSERT dbo.t_Unotp_Investor_Folio (c_Folio, c_Pan, d_Dob, c_Name, c_Gender, c_Address, f_Doc_Pan, f_Doc_Photo, f_Doc_Poa, c_Note, c_Created_By)
SELECT v.f, v.p, v.d, v.n, v.g, v.a, v.dp, v.dh, v.dq, N'CKYC available with us', 'DEVSEED' FROM (VALUES
    (N'TS003027', N'XXXXA1001A', N'1988-08-14', N'SHIVAPRASAD SUBHASH TERSE', N'Male', N'Flat 12, Shantiniketan CHS, Baner Road, Pune, Maharashtra 411045', 1, 1, 1),
    (N'MF0051187', N'XXXXB1002B', N'1988-08-14', N'RAHUL SUDHIR TAMBE', N'Male', N'', 1, 0, 0),
    (N'MF0084456', N'XXXXH1008H', N'1988-08-14', N'MEERA ANIL JOSHI', N'Female', N'9 Gulmohar Apartments, Aundh, Pune, Maharashtra 411007', 0, 1, 1),
    (N'MF0062210', N'XXXXF1006F', N'1988-08-14', N'NEHA SURESH KULKARNI', N'Female', N'22 Sai Residency, Kothrud, Pune, Maharashtra 411038', 1, 1, 1),
    (N'MF0062211', N'XXXXF1006F', N'1988-08-14', N'NEHA SURESH KULKARNI', N'Female', N'22 Sai Residency, Kothrud, Pune, Maharashtra 411038', 1, 1, 0),
    (N'MF0073345', N'XXXXG1007G', NULL, N'PRAKASH VINAYAK GOKHALE', N'Male', N'5 Laxmi Niwas, Dombivli East, Thane, Maharashtra 421201', 1, 1, 1)
) v (f, p, d, n, g, a, dp, dh, dq)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_Investor_Folio x WHERE x.c_Folio = v.f);
GO
INSERT dbo.t_Unotp_Broker_Mst (c_Code, c_Name, c_Created_By)
SELECT v.c, v.n, 'DEVSEED' FROM (VALUES
    (N'BR10021', N'Sahyadri Investment Services'),
    (N'BR10874', N'Deccan Wealth Advisors'),
    (N'BR11250', N'Konkan Financial Services'),
    (N'BR11903', N'Nagpur Capital Partners'),
    (N'BR12388', N'Godavari Securities')
) v (c, n) WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_Broker_Mst x WHERE x.c_Code = v.c);
GO
INSERT dbo.t_Unotp_Staff_Mst (c_Code, c_Name, c_Created_By)
SELECT v.c, v.n, 'DEVSEED' FROM (VALUES
    (N'100002225', N'Shivaprasad Terse'),
    (N'E10428', N'Nikhil Ramesh Bhosale'),
    (N'E20915', N'Sneha Arun Kulkarni'),
    (N'E31077', N'Farhan Iqbal Shaikh')
) v (c, n) WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_Staff_Mst x WHERE x.c_Code = v.c);
GO
INSERT dbo.t_Unotp_Ifsc_Mst (c_Ifsc, c_Bank, c_Branch, c_Micr, c_Created_By)
SELECT v.i, v.b, v.br, v.m, 'DEVSEED' FROM (VALUES
    (N'HDFC0000521', N'HDFC Bank', N'Andheri East, Mumbai', N'400240015'),
    (N'HDFC0000123', N'HDFC Bank', N'Baner, Pune', N'411240012'),
    (N'ICIC0000104', N'ICICI Bank', N'Fort, Mumbai', N'400229002'),
    (N'SBIN0000575', N'State Bank of India', N'Shivajinagar, Pune', N'411002003'),
    (N'UTIB0000014', N'Axis Bank', N'Naupada, Thane', N'400211003')
) v (i, b, br, m) WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_Ifsc_Mst x WHERE x.c_Ifsc = v.i);
GO
INSERT dbo.t_Unotp_Pincode_Mst (c_Pin_Code, c_District, c_State, c_Created_By)
SELECT v.p, v.d, v.s, 'DEVSEED' FROM (VALUES
    (N'400001', N'Mumbai', N'Maharashtra'),
    (N'400050', N'Mumbai', N'Maharashtra'),
    (N'411001', N'Pune', N'Maharashtra'),
    (N'411045', N'Pune', N'Maharashtra'),
    (N'110001', N'New Delhi', N'Delhi'),
    (N'560001', N'Bengaluru Urban', N'Karnataka'),
    (N'600001', N'Chennai', N'Tamil Nadu'),
    (N'700001', N'Kolkata', N'West Bengal'),
    (N'500001', N'Hyderabad', N'Telangana'),
    (N'380001', N'Ahmedabad', N'Gujarat'),
    (N'302001', N'Jaipur', N'Rajasthan'),
    (N'226001', N'Lucknow', N'Uttar Pradesh'),
    (N'682001', N'Ernakulam', N'Kerala'),
    (N'751001', N'Khordha', N'Odisha')
) v (p, d, s) WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_Pincode_Mst x WHERE x.c_Pin_Code = v.p);
GO
-- The rate card is not dev data: db/007 loads the published chart.
