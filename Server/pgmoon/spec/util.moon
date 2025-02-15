
HOST = "127.0.0.1"
PORT = "9999"
USER = "postgres"
PASSWORD = "pgmoon"
DB = "pgmoon_test"

shell_escape = (str) -> str\gsub "'", "'\\''"

psql = (query) ->
  os.execute "PGHOST='#{shell_escape HOST}' PGPORT='#{shell_escape PORT}' PGUSER='#{shell_escape USER}' PGPASSWORD='#{shell_escape PASSWORD}' psql -c '#{query}'"


{:psql, :HOST, :PORT, :USER, :PASSWORD, :DB }
