# Esatto Data Access

Micro-ORM for SQL Server

    class SomeDal
    {
        private readonly DbConf con;

        public SomeDal(ILogger<SomeDal> logger)
        {
            this.con = new DbConf(logger, "Server=localhost;Database=Esatto;User Id=sa;Password=password;", "schemaName");
        }
        
        public void CreateUpdateFrobble(int clientId, Frobble Frobble)
        {
            dynamic sp = con.StoredProcedure("CreateUpdateFrobble");
            sp.ZX_FrobbleID = Frobble.IsPersisted ? Frobble.ID : (object)SqlDbType.Int;
            sp.ClientID = clientId;
            sp.InventorySetID = Frobble.InventorySet.ID;
            sp.Execute();
            Frobble.ID = sp.ZX_FrobbleID;
        }

        internal void SetFrobbleLocation(int FrobbleId, int clientId, FrobbleLocation location)
        {
            con.ExecuteStoredProcedure("SetFrobbleLocation", new
            {
                FrobbleId,
                clientId,
                packagerId,
                locationName
            });
        }
        
        public List<Widget> GetAllWidgets(int ClientID, int? WidgetID)
        {
            using var reader = con.ExecuteStoredProcedureReader("GetAllWidgets", new { ClientID, WidgetID });
            return reader.GetList<Widget>("g_", FillWidget);
        }

        private void FillWidget(Widget g, ResultSet rs)
        {
            g.GroupPolicy = rs.GetPartialObject<WidgetPolicyHeader>("gp_", FillWidgetPolicyHeader);
        }
    }