var modelDb = app.ActiveModelDb;
var table = modelDb.Table("BUILDINGS");
var filter1 = "";
table.StartQuery(filter1);
var read;
print("Идентификатор;Описание");
while (read = table.Next()) {
    print("ID " + read.ID + ";" + read.DESCRIPTION);
}
table.EndQuery();


gc();
