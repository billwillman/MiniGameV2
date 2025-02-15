-- Table: public.userlogin

-- DROP TABLE IF EXISTS public.userlogin;

CREATE TABLE IF NOT EXISTS public.userlogin
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    username character varying COLLATE pg_catalog."default" NOT NULL,
    password character varying COLLATE pg_catalog."default" DEFAULT ''::character varying,
    "isLock" bit(1) NOT NULL DEFAULT (1)::bit(1),
    "lockEndTime" time without time zone,
    CONSTRAINT user_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.userlogin
    OWNER to postgres;